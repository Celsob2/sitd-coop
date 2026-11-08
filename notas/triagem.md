# Triagem estática: `C:\Program Files (x86)\Steam\steamapps\common\Spark in the Dark`

Gerado em 2026-11-08T14:22:40+00:00 por triagem.py 1.1. O alvo **não foi executado**.
Arquivos examinados: 552 (6.7 GiB); leitura limitada a primeiros 64 KiB e últimos 8 KiB.

## Engine/runtime candidatos

### Unity (scripting Mono/.NET) 2022.3.62f3 — forte (assinatura lida)

Evidências: `UnityPlayer.dll`; `Spark in the Dark_Data/Managed/`; `Spark in the Dark_Data\globalgamemanagers`; `Spark in the Dark_Data\Managed\Assembly-CSharp-firstpass.dll (assembly .NET em Managed/)`; `Spark in the Dark_Data\Managed\Assembly-CSharp.dll (assembly .NET em Managed/)`; `Spark in the Dark_Data\Managed\UnityEngine.AccessibilityModule.dll (assembly .NET em Managed/)`; `Spark in the Dark_Data\Managed\UnityEngine.AIModule.dll (assembly .NET em Managed/)`; `Spark in the Dark_Data\Managed\UnityEngine.AndroidJNIModule.dll (assembly .NET em Managed/)`; `Spark in the Dark_Data\Managed\UnityEngine.AnimationModule.dll (assembly .NET em Managed/)`; `Spark in the Dark_Data\Managed\UnityEngine.ARModule.dll (assembly .NET em Managed/)`; `Spark in the Dark_Data\Managed\UnityEngine.AssetBundleModule.dll (assembly .NET em Managed/)`; `Spark in the Dark_Data\Managed\UnityEngine.AudioModule.dll (assembly .NET em Managed/)`

Próximos passos:
- REA inspect_managed_artifact ou ILSpy/ilspycmd em *_Data/Managed/Assembly-CSharp.dll
- Mods: BepInEx 5 + HarmonyX; inspeção em runtime: UnityExplorer
- Recursos: AssetRipper/AssetStudio/UABEA (uso local)
- Referência (na pasta da skill): `references/engines-e-formatos.md#unity`

## Anomalias e alertas

Resolver ou registrar antes de seguir para decompilação/modificação.

- a instalação já contém mod loader/hooks: o manifesto registrará um estado **modificado**; para ter uma base limpa, o usuário pode reinstalar ou usar 'verificar integridade' da loja (isso remove os mods) e montar o loader só na cópia de trabalho
- 2 arquivos de nomes diferentes têm conteúdo idêntico (`grpc\mscordaccore.dll`, `grpc\mscordaccore_amd64_amd64_8.0.2225.52707.dll`); pode ser duplicação legítima, stub ou instalação corrompida

## Proteções detectadas

Nenhum marcador conhecido encontrado (isso não prova ausência de proteção).

## Integrações

- **Steamworks API (integração com a Steam; não é DRM por si só)**: `Spark in the Dark_Data\Plugins\x86_64\steam_api64.dll`

## Mod loaders e hooks presentes

- **Unity Doorstop (BepInEx)**: `doorstop_config.ini`
- **possível DLL proxy (winhttp.dll, usada pelo Doorstop/BepInEx)**: `winhttp.dll`
- **BepInEx**: `BepInEx`, `BepInEx\config\BepInEx.cfg`, `BepInEx\core\BepInEx.Core.dll`, `BepInEx\core\BepInEx.Core.xml`, `BepInEx\core\BepInEx.Preloader.Core.dll`, `BepInEx\core\BepInEx.Preloader.Core.xml`, `BepInEx\core\BepInEx.Unity.Common.dll`, `BepInEx\core\BepInEx.Unity.Common.xml`

## Maiores binários

| Caminho | Formato | Arquitetura | Detalhes |
| --- | --- | --- | --- |
| `UnityPlayer.dll` (29.7 MiB) | PE | x86-64 | DLL, console |
| `Spark in the Dark_Data\Plugins\x86_64\Galaxy64.dll` (13.7 MiB) | PE | x86-64 | DLL, console |
| `grpc\System.Private.CoreLib.dll` (12.6 MiB) | PE | x86-64 | .NET/CLI, DLL, console |
| `BepInEx\plugins\xmod\ModFramework.dll` (10.5 MiB) | PE | AnyCPU (IL) | .NET/CLI, DLL, console |
| `grpc\System.Private.Xml.dll` (7.6 MiB) | PE | x86-64 | .NET/CLI, DLL, console |
| `MonoBleedingEdge\EmbedRuntime\mono-2.0-bdwgc.dll` (7.5 MiB) | PE | x86-64 | DLL, GUI |
| `DWrite.dll` (6.1 MiB) | PE | x86-64 | DLL, GUI |
| `grpc\coreclr.dll` (4.8 MiB) | PE | x86-64 | DLL, GUI |
| `Spark in the Dark_Data\Managed\mscorlib.dll` (4.4 MiB) | PE | AnyCPU (IL) | .NET/CLI, DLL, console |
| `grpc\System.Linq.Expressions.dll` (3.5 MiB) | PE | x86-64 | .NET/CLI, DLL, console |
| `Spark in the Dark_Data\Managed\System.Xml.dll` (3.0 MiB) | PE | AnyCPU (IL) | .NET/CLI, DLL, console |
| `grpc\System.Data.Common.dll` (2.7 MiB) | PE | x86-64 | .NET/CLI, DLL, console |
| `Spark in the Dark_Data\Managed\System.dll` (2.5 MiB) | PE | AnyCPU (IL) | .NET/CLI, DLL, console |
| `grpc\Microsoft.AspNetCore.Server.Kestrel.Core.dll` (2.2 MiB) | PE | x86-64 | .NET/CLI, DLL, console |
| `grpc\Microsoft.DiaSymReader.Native.amd64.dll` (2.1 MiB) | PE | x86-64 | DLL, GUI |

## Formatos reconhecidos por assinatura

- pe: 478

## Identidade dos arquivos-chave (SHA-256 do arquivo completo)

| Caminho | Tamanho | SHA-256 |
| --- | --- | --- |
| `DWrite.dll` | 6429184 | `69352751162bf7e2adcd6f8ba2eed38ab4a150a605926d2cc44eb650e188ec31` |
| `Spark in the Dark.exe` | 666624 | `e520deab9831cae42921e2ec6cdbd2d91020eca3a2745f2c5e41889be122bb48` |
| `UnityCrashHandler64.exe` | 1185192 | `ca93c9d370e6e595bc402dde42591eb8827783c0a35c87df7ebaa58d530cd725` |
| `UnityPlayer.dll` | 31100848 | `dc005461655b67912029eea6bcc303fe244c3a336e2046107f934b901ab9ce2b` |
| `winhttp.dll` | 26112 | `d708c08f7855bbc3963170ede3a8e560cf8b2a835e28f4b6db32ad1d9400cb60` |
| `BepInEx\core\0Harmony.dll` | 263168 | `9710c782b13742fa23b9fc2c3608263ba6f1d08ef1342898fc26a8f539d3f247` |
| `BepInEx\core\AssetRipper.Primitives.dll` | 22016 | `7b48d6f1d13cdfdfe346e687d702884ade6cacf7f91b087249d680c80f56cad6` |
| `BepInEx\core\BepInEx.Core.dll` | 131072 | `b05275485c3a1f7f93c52ca9439a22afba1798948dbaf99a1c6095dee5f9c9a4` |
| `BepInEx\core\BepInEx.Preloader.Core.dll` | 48640 | `dc9515abe1203c49fb06136d7e37b6c071191ba194369f2edb1f3033fbb31d9c` |
| `BepInEx\core\BepInEx.Unity.Common.dll` | 9728 | `1c252d727acd866f2455a47673dbe318c90f5552fa1658f6784161a769faed2f` |
| `BepInEx\core\BepInEx.Unity.Mono.dll` | 51712 | `1f8b0b7c0c39e297f92d121d6322394bcc6286c3746ec9ca093edd3b639d0b15` |
| `BepInEx\core\BepInEx.Unity.Mono.Preloader.dll` | 34816 | `ce4783f3114b5acbce433d4fe8124b6a5ada0ae7baa2567736b5b224f6f74604` |
| `BepInEx\core\Mono.Cecil.dll` | 339456 | `7ae470288fff4a402899c254d0a76cefef55877f5c54f96e83c797cc5bb6e2f6` |
| `BepInEx\core\Mono.Cecil.Mdb.dll` | 43008 | `5896d1898f616701fff18f3b2c71e6b844d2390ef9f41e1c5fccce8cb27c698e` |
| `BepInEx\core\Mono.Cecil.Pdb.dll` | 86528 | `174db44a067f58561510af746f3caeb032037762c57a31c8d9ee32db25174984` |
| `BepInEx\core\Mono.Cecil.Rocks.dll` | 27648 | `54ac539fb5ddc8b44c0e9acd0fcb7324f89d1a072edf8ebc1b06dd691e3d3927` |
| `BepInEx\core\MonoMod.RuntimeDetour.dll` | 108032 | `6120a4a1fa3287b149f408eeac04c017c6453e8fd69dbc5d12f1489c087385ab` |
| `BepInEx\core\MonoMod.Utils.dll` | 188416 | `c764fa0141e3b9308ae25808baea11feabc0b4843ecfc28ec5c5cc470d6ab420` |
| `BepInEx\core\SemanticVersioning.dll` | 35328 | `8603f22e2d552a37a9b59474e952fad4320b53b20a902ab639e7a2adddf4fa3e` |
| `BepInEx\plugins\SparkCoop\SparkCoop.dll` | 86016 | `f1a23d127bc5cd3a17a1c81aa3a27a3dcf46696bff713852a42b7da89ce6d234` |
| `BepInEx\plugins\xmod\0Harmony.dll` | 263168 | `9710c782b13742fa23b9fc2c3608263ba6f1d08ef1342898fc26a8f539d3f247` |
| `BepInEx\plugins\xmod\ModFramework.dll` | 11023872 | `f4a1b2e7965d038d5b974f0decfa532d09e450099f945c4591a87a56f1b8dcdf` |
| `BepInEx\plugins\xmod\SparkInTheDark.dll` | 233984 | `01c3fc400f9c6fe1118324154dbf1c6e964c8d20ef71b981e9230ad51600a2ff` |
| `grpc\aspnetcorev2_inprocess.dll` | 383520 | `7bd1b53e48944ee1ade4c9593959112a602cf457213452e7afa0b7f5150f164b` |
| `grpc\clretwrc.dll` | 311048 | `01aa7d74d16ae2ff436ae7ac84b87b2f15782bc0967b614a39a104966d703fc3` |
| `grpc\clrgc.dll` | 668464 | `965f6c73773166a6f83cd90d8bae7d6f110b0c833763944486b60c37d1e98052` |
| `grpc\clrjit.dll` | 1778440 | `35b450153b686e94821d3aa82ef6669f57f60088c1d0cc4af9d967107a7179f9` |
| `grpc\coreclr.dll` | 4995376 | `36e658ced657f4b29526d0abf8a2b8e208c20a2730512f25b80d3eec7955920f` |
| `grpc\createdump.exe` | 71992 | `42fec70d833344ae2939dd357b57d756505b6054c23c788ec400ea0ee4a5c933` |
| `grpc\ExternalGrpcHost.dll` | 30208 | `f87cc875b378306f7a313f0682bc1c2103e9ba4f42de56967dda9e22abe77bf4` |
| `grpc\ExternalGrpcHost.exe` | 151552 | `1056caa92510ed2b7953d0736b920e2352af0d6e7b64fc8d8d4f3fa2accfcfda` |
| `grpc\Google.Protobuf.dll` | 489568 | `b71be7a4e721d1b79456a0b52602a01a4414eb09bab4a04400f6f9d8a3895157` |
| `grpc\Grpc.AspNetCore.Server.ClientFactory.dll` | 31840 | `19a4c689581e5dd61f27e8fe7504b3b56f93f62bd889f4ca8f42833a9f7ba519` |
| `grpc\Grpc.AspNetCore.Server.dll` | 159328 | `aa897d117f73e2cd10c6f8ed7dbf9c532d631fa6e92c13a95de174150c387b81` |
| `grpc\Grpc.Core.Api.dll` | 70240 | `c14ac4d4c97e03cf1f05892caeb6346457ac25c8b1b7ee3e12a720fcaa1fb2e2` |
| `grpc\Grpc.Net.Client.dll` | 359520 | `75fe0ec43986ef6e78cf41268728909b9819263273fb1e84146c1c6ef58e0028` |
| `grpc\Grpc.Net.ClientFactory.dll` | 54368 | `2c0a237bf93aafe7d85636ae0d7aac4b93fa70ffe1014da6ed9018292846dfbc` |
| `grpc\Grpc.Net.Common.dll` | 22112 | `c31a578aed06bd7723ec24139882bbd10e18b79a6fe190482c771113ce32bd43` |
| `grpc\hostfxr.dll` | 366856 | `aaa156793b9b22ece28553b21a414036da1aba7121ea9bc01ff481d1fb25911c` |
| `grpc\hostpolicy.dll` | 407312 | `a0b14a705c5ae27d83de5736a2b45aa0d0ebc7f497340301749bf9e44a1c338f` |
| `grpc\Microsoft.AspNetCore.Antiforgery.dll` | 121104 | `ac6224d9b945c011158c2b7e30c501d585fd550d5a25309c8197721a6e0a32fc` |
| `grpc\Microsoft.AspNetCore.Authentication.Abstractions.dll` | 67848 | `1ac66a3c06d3f89865de6c182da3db0f7d3cd97b0a9d91ebf1b7ebee29a1b21b` |
| `grpc\Microsoft.AspNetCore.Authentication.BearerToken.dll` | 71952 | `54e8458a5961b600a50ef6e85551f0b04e385785fe6b11dfff5ddcf24d855c28` |
| `grpc\Microsoft.AspNetCore.Authentication.Cookies.dll` | 112904 | `c235da18c328e99bbed88e6c039a1aca412525e5d38dc968316a0426e5e6d3a2` |
| `grpc\Microsoft.AspNetCore.Authentication.Core.dll` | 92424 | `bb55757c5e085c62e84830bbf60a10330fc74a87ff7fd6200cfc3ba01c7a7e8a` |
| `grpc\Microsoft.AspNetCore.Authentication.dll` | 158008 | `eb1ff1a321ef0c8266ae52babc8cecbcb2098987ba0fa3e1105a2d95a7294d5b` |
| `grpc\Microsoft.AspNetCore.Authentication.OAuth.dll` | 100616 | `7ecf06b750492e2d08d335a414689990187d4d376ae8a75c082bfdd78f098ef3` |
| `grpc\Microsoft.AspNetCore.Authorization.dll` | 108856 | `aa36b80ad767b777def82533e621c738f589512b8914605c44523d438029917f` |
| `grpc\Microsoft.AspNetCore.Authorization.Policy.dll` | 80136 | `6f7fa5e90f2e1ee37f2fe276cc636c1f1911456672f6537ca76cdc485ec48a4a` |
| `grpc\Microsoft.AspNetCore.Components.Authorization.dll` | 55560 | `bb8132bb6efd26b09917bffbe8cfa91d5061485549f15873ea0f9b20085d6264` |
| `grpc\Microsoft.AspNetCore.Components.dll` | 825648 | `ed7b867f131f1a35c289b65329ea86bfe22d7685e64c2ec78246f44abc23ab37` |
| `grpc\Microsoft.AspNetCore.Components.Endpoints.dll` | 723248 | `efedbeec0dbb0e357e979254efb4fe6765d8de27329a947894ee50bd077c70ab` |
| `grpc\Microsoft.AspNetCore.Components.Forms.dll` | 96560 | `c61f7354ba4cbd9b7ece8c375b34f620aab5f01ffa709bb63eb42d431dd1fb01` |
| `grpc\Microsoft.AspNetCore.Components.Server.dll` | 866616 | `5d85b51b4513a3a45bcc3401bfb95934ee4d3845234745e71ac4957bc7a9fdb6` |
| `grpc\Microsoft.AspNetCore.Components.Web.dll` | 391480 | `1afd82576899ebb2ea6d3977fcd9653de0f8c77239483228edd1bdefbfe11e76` |
| `grpc\Microsoft.AspNetCore.Connections.Abstractions.dll` | 67848 | `08640dad1b18d3c191b398fe4a94c45820d90a96095c6f93b0d2373c2efd6159` |
| `grpc\Microsoft.AspNetCore.CookiePolicy.dll` | 55560 | `0ccf61193ce84dc0acd85886c80d6196e7c7a9eb7a4e31ab345a381de386761e` |
| `grpc\Microsoft.AspNetCore.Cors.dll` | 92464 | `e248118c7d95105f52d97a90d6576b7bdee0e5d2e8c9cbe090334b5a48c5f409` |
| `grpc\Microsoft.AspNetCore.Cryptography.Internal.dll` | 108808 | `1562e347d78c80beb1e8046a1a6f9e4e3615258d2e1711cfee1d64998ea24d78` |
| `grpc\Microsoft.AspNetCore.Cryptography.KeyDerivation.dll` | 47376 | `f42d5acedd8fd2d0a2244755f7dc39dcf78a7d7103306c2dd07a1cd824280fe6` |

Convenções de nome são pistas; confirme com a ferramenta indicada antes de concluir.
