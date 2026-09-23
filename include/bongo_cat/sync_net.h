#ifndef BONGO_CAT_SYNC_NET_H
#define BONGO_CAT_SYNC_NET_H

#include <stdbool.h>
#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

typedef struct {
    bool enabled;
    char target_ip[64];
    uint16_t target_port;
    bool broadcast;
} BongoCatSyncNetConfig;

bool bongo_cat_sync_net_init(const BongoCatSyncNetConfig *config);
void bongo_cat_sync_net_set_target(const char *ip, uint16_t port);
void bongo_cat_sync_net_send_tap(void);
void bongo_cat_sync_net_shutdown(void);

#ifdef __cplusplus
}
#endif

#endif /* BONGO_CAT_SYNC_NET_H */
