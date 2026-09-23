using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace BongoPatcher
{
    class Program
    {
        static int Main(string[] args)
        {
            try
            {
                string managedDir = args.Length > 0 ? args[0] : "BongoCat-steam/BongoCat_Data/Managed";
                string origDllPath = Path.Combine(managedDir, "Assembly-CSharp.dll.orig");
                string targetDllPath = Path.Combine(managedDir, "Assembly-CSharp.dll");
                string syncDllPath = Path.Combine(managedDir, "BongoSync.dll");

                if (!File.Exists(origDllPath))
                {
                    Console.WriteLine("Error: original assembly not found at: " + origDllPath);
                    return 1;
                }

                if (!File.Exists(syncDllPath))
                {
                    Console.WriteLine("Error: BongoSync.dll not found at: " + syncDllPath);
                    return 1;
                }

                Console.WriteLine("Reading BongoSync assembly...");
                var syncAssembly = AssemblyDefinition.ReadAssembly(syncDllPath);
                var syncReceiverType = syncAssembly.MainModule.GetType("BongoCat.Sync.BongoSyncReceiver");
                if (syncReceiverType == null)
                {
                    Console.WriteLine("Error: BongoCat.Sync.BongoSyncReceiver not found in BongoSync.dll");
                    return 1;
                }

                var initMethodDef = syncReceiverType.Methods.First(m => m.Name == "Init");
                var popTapsMethodDef = syncReceiverType.Methods.First(m => m.Name == "PopTaps");
                var shutdownMethodDef = syncReceiverType.Methods.First(m => m.Name == "Shutdown");

                Console.WriteLine("Reading Assembly-CSharp.dll.orig...");
                var readerParams = new ReaderParameters { ReadSymbols = false };
                var targetAssembly = AssemblyDefinition.ReadAssembly(origDllPath, readerParams);
                var module = targetAssembly.MainModule;

                var initRef = module.ImportReference(initMethodDef);
                var popTapsRef = module.ImportReference(popTapsMethodDef);
                var shutdownRef = module.ImportReference(shutdownMethodDef);

                var hookType = module.GetType("BongoCat.OSSpecific.GlobalKeyHook");
                if (hookType == null)
                {
                    Console.WriteLine("Error: GlobalKeyHook type not found!");
                    return 1;
                }

                var keysDownField = hookType.Fields.First(f => f.Name == "_keysDown");

                // 1. Patch Awake() -> BongoSyncReceiver.Init()
                var awakeMethod = hookType.Methods.First(m => m.Name == "Awake");
                var awakeIl = awakeMethod.Body.GetILProcessor();
                var awakeRet = awakeMethod.Body.Instructions.Last(i => i.OpCode == OpCodes.Ret);
                awakeIl.InsertBefore(awakeRet, Instruction.Create(OpCodes.Call, initRef));
                Console.WriteLine("Patched GlobalKeyHook.Awake()");

                // 2. Patch Update() -> this._keysDown += BongoSyncReceiver.PopTaps()
                var updateMethod = hookType.Methods.First(m => m.Name == "Update");
                var updateIl = updateMethod.Body.GetILProcessor();
                
                // Find after UpdateTriggerCooldown()
                var cooldownCall = updateMethod.Body.Instructions.First(i => 
                    i.OpCode == OpCodes.Call && 
                    i.Operand is MethodReference mr && 
                    mr.Name == "UpdateTriggerCooldown");

                var targetInsertPoint = cooldownCall.Next; // instruction right after call

                // Insert:
                // ldarg.0
                // ldarg.0
                // ldfld int32 GlobalKeyHook::_keysDown
                // call int32 BongoSyncReceiver::PopTaps()
                // add
                // stfld int32 GlobalKeyHook::_keysDown
                var ins1 = Instruction.Create(OpCodes.Ldarg_0);
                var ins2 = Instruction.Create(OpCodes.Ldarg_0);
                var ins3 = Instruction.Create(OpCodes.Ldfld, keysDownField);
                var ins4 = Instruction.Create(OpCodes.Call, popTapsRef);
                var ins5 = Instruction.Create(OpCodes.Add);
                var ins6 = Instruction.Create(OpCodes.Stfld, keysDownField);

                updateIl.InsertBefore(targetInsertPoint, ins1);
                updateIl.InsertBefore(targetInsertPoint, ins2);
                updateIl.InsertBefore(targetInsertPoint, ins3);
                updateIl.InsertBefore(targetInsertPoint, ins4);
                updateIl.InsertBefore(targetInsertPoint, ins5);
                updateIl.InsertBefore(targetInsertPoint, ins6);
                Console.WriteLine("Patched GlobalKeyHook.Update()");

                // 3. Patch OnApplicationQuit() -> BongoSyncReceiver.Shutdown()
                var quitMethod = hookType.Methods.FirstOrDefault(m => m.Name == "OnApplicationQuit");
                if (quitMethod != null)
                {
                    var quitIl = quitMethod.Body.GetILProcessor();
                    var quitRet = quitMethod.Body.Instructions.Last(i => i.OpCode == OpCodes.Ret);
                    quitIl.InsertBefore(quitRet, Instruction.Create(OpCodes.Call, shutdownRef));
                    Console.WriteLine("Patched GlobalKeyHook.OnApplicationQuit()");
                }

                // Write patched assembly
                var tempOutput = targetDllPath + ".tmp";
                targetAssembly.Write(tempOutput);
                File.Delete(targetDllPath);
                File.Move(tempOutput, targetDllPath);

                Console.WriteLine("Successfully wrote patched assembly to: " + targetDllPath);
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Patching failed with exception: " + ex);
                return 1;
            }
        }
    }
}
