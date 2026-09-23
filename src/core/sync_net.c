#include "bongo_cat/sync_net.h"
#include "yyjson.h"

#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#ifdef _WIN32
#include <winsock2.h>
#include <ws2tcpip.h>
#else
#include <sys/types.h>
#include <sys/socket.h>
#include <netinet/in.h>
#include <arpa/inet.h>
#include <unistd.h>
#include <fcntl.h>
#include <pwd.h>
#endif

static int s_socket_fd = -1;
static struct sockaddr_in s_target_addr;
static bool s_initialized = false;
static bool s_enabled = false;
static BongoCatSyncNetConfig s_current_config;

static void load_config_file(BongoCatSyncNetConfig *config, const char *path) {
    if (!path) return;
    FILE *f = fopen(path, "rb");
    if (!f) return;
    fseek(f, 0, SEEK_END);
    long len = ftell(f);
    if (len <= 0 || len > 65536) {
        fclose(f);
        return;
    }
    fseek(f, 0, SEEK_SET);
    char *buf = malloc(len + 1);
    if (!buf) {
        fclose(f);
        return;
    }
    size_t read_bytes = fread(buf, 1, len, f);
    fclose(f);
    buf[read_bytes] = '\0';

    yyjson_doc *doc = yyjson_read(buf, read_bytes, 0);
    free(buf);
    if (!doc) return;

    yyjson_val *root = yyjson_doc_get_root(doc);
    if (yyjson_is_obj(root)) {
        yyjson_val *enabled_val = yyjson_obj_get(root, "enabled");
        if (enabled_val && yyjson_is_bool(enabled_val)) {
            config->enabled = yyjson_get_bool(enabled_val);
        }
        yyjson_val *ip_val = yyjson_obj_get(root, "target_ip");
        if (!ip_val) ip_val = yyjson_obj_get(root, "targetIp");
        if (!ip_val) ip_val = yyjson_obj_get(root, "ip");
        if (ip_val && yyjson_is_str(ip_val)) {
            const char *ip_str = yyjson_get_str(ip_val);
            if (ip_str && ip_str[0] != '\0') {
                strncpy(config->target_ip, ip_str, sizeof(config->target_ip) - 1);
                config->target_ip[sizeof(config->target_ip) - 1] = '\0';
                config->broadcast = (strcmp(config->target_ip, "255.255.255.255") == 0);
            }
        }
        yyjson_val *port_val = yyjson_obj_get(root, "target_port");
        if (!port_val) port_val = yyjson_obj_get(root, "targetPort");
        if (!port_val) port_val = yyjson_obj_get(root, "port");
        if (port_val && yyjson_is_int(port_val)) {
            int port = yyjson_get_int(port_val);
            if (port > 0 && port <= 65535) {
                config->target_port = (uint16_t)port;
            }
        }
    }
    yyjson_doc_free(doc);
}

static void discover_default_config(BongoCatSyncNetConfig *config) {
    config->enabled = true;
    config->target_port = 39824;
    config->broadcast = true;
    strncpy(config->target_ip, "255.255.255.255", sizeof(config->target_ip) - 1);

    // 1. Try local sync.json in current directory
    load_config_file(config, "sync.json");

    // 2. Try macOS standard user config location
#if defined(__APPLE__)
    const char *home = getenv("HOME");
    if (!home) {
        struct passwd *pw = getpwuid(getuid());
        if (pw) home = pw->pw_dir;
    }
    if (home) {
        char user_cfg[1024];
        snprintf(user_cfg, sizeof(user_cfg), "%s/Library/Application Support/BongoCat/config/sync.json", home);
        load_config_file(config, user_cfg);
    }
#elif defined(_WIN32)
    const char *appdata = getenv("LOCALAPPDATA");
    if (appdata) {
        char user_cfg[1024];
        snprintf(user_cfg, sizeof(user_cfg), "%s\\BongoCat\\config\\sync.json", appdata);
        load_config_file(config, user_cfg);
    }
#endif

    // 3. Environment variables take precedence if present
    const char *env_ip = getenv("BONGO_SYNC_IP");
    if (env_ip && env_ip[0] != '\0') {
        strncpy(config->target_ip, env_ip, sizeof(config->target_ip) - 1);
        config->target_ip[sizeof(config->target_ip) - 1] = '\0';
        config->broadcast = (strcmp(config->target_ip, "255.255.255.255") == 0);
    }

    const char *env_port = getenv("BONGO_SYNC_PORT");
    if (env_port && env_port[0] != '\0') {
        int port = atoi(env_port);
        if (port > 0 && port <= 65535) {
            config->target_port = (uint16_t)port;
        }
    }
}

bool bongo_cat_sync_net_init(const BongoCatSyncNetConfig *config) {
    if (s_initialized) return true;

    BongoCatSyncNetConfig cfg;
    if (config) {
        cfg = *config;
    } else {
        discover_default_config(&cfg);
    }

    if (!cfg.enabled) {
        s_enabled = false;
        s_initialized = true;
        return true;
    }

#ifdef _WIN32
    WSADATA wsa;
    if (WSAStartup(MAKEWORD(2, 2), &wsa) != 0) return false;
#endif

    s_socket_fd = socket(AF_INET, SOCK_DGRAM, 0);
    if (s_socket_fd < 0) {
        return false;
    }

    // Set non-blocking socket so network latency never blocks input rendering
#ifdef _WIN32
    u_long mode = 1;
    ioctlsocket(s_socket_fd, FIONBIO, &mode);
#else
    int flags = fcntl(s_socket_fd, F_GETFL, 0);
    if (flags >= 0) {
        fcntl(s_socket_fd, F_SETFL, flags | O_NONBLOCK);
    }
#endif

    if (cfg.broadcast) {
        int broadcast_enable = 1;
        setsockopt(s_socket_fd, SOL_SOCKET, SO_BROADCAST,
                   (const char *)&broadcast_enable, sizeof(broadcast_enable));
    }

    memset(&s_target_addr, 0, sizeof(s_target_addr));
    s_target_addr.sin_family = AF_INET;
    s_target_addr.sin_port = htons(cfg.target_port > 0 ? cfg.target_port : 39824);

    const char *ip = (cfg.target_ip[0] != '\0') ? cfg.target_ip : "255.255.255.255";
    if (inet_pton(AF_INET, ip, &s_target_addr.sin_addr) <= 0) {
        s_target_addr.sin_addr.s_addr = htonl(INADDR_BROADCAST);
        cfg.broadcast = true;
    }

    s_current_config = cfg;
    s_enabled = true;
    s_initialized = true;

    printf("[BongoSync] LAN Tap Sync initialized -> %s:%d (%s)\n",
           cfg.target_ip, cfg.target_port,
           cfg.broadcast ? "Broadcast mode" : "Direct Unicast mode");
    fflush(stdout);

    return true;
}

void bongo_cat_sync_net_set_target(const char *ip, uint16_t port) {
    if (!ip || ip[0] == '\0') return;

    BongoCatSyncNetConfig cfg;
    if (s_initialized) {
        cfg = s_current_config;
    } else {
        discover_default_config(&cfg);
    }

    strncpy(cfg.target_ip, ip, sizeof(cfg.target_ip) - 1);
    cfg.target_ip[sizeof(cfg.target_ip) - 1] = '\0';
    cfg.target_port = (port > 0) ? port : (cfg.target_port > 0 ? cfg.target_port : 39824);
    cfg.broadcast = (strcmp(cfg.target_ip, "255.255.255.255") == 0);
    cfg.enabled = true;

    if (s_initialized) {
        bongo_cat_sync_net_shutdown();
    }
    bongo_cat_sync_net_init(&cfg);
}

void bongo_cat_sync_net_send_tap(void) {
    if (!s_initialized) {
        if (!bongo_cat_sync_net_init(NULL)) return;
    }
    if (!s_enabled || s_socket_fd < 0) return;

    static const char packet[] = "TAP:1";
    sendto(s_socket_fd, packet, sizeof(packet) - 1, 0,
           (struct sockaddr *)&s_target_addr, sizeof(s_target_addr));
}

void bongo_cat_sync_net_shutdown(void) {
    if (s_socket_fd >= 0) {
#ifdef _WIN32
        closesocket(s_socket_fd);
        WSACleanup();
#else
        close(s_socket_fd);
#endif
        s_socket_fd = -1;
    }
    s_initialized = false;
    s_enabled = false;
}
