# v6 build fix

GitHub Actions showed:
`Web/NativeBridge.cs(241,41): CS0103: The name 'Path' does not exist in the current context`

v6 adds explicit `using System.IO;` to filesystem-using source files and a
`GlobalUsings.cs` containing `global using System.IO;`.

The local-only/native media import design from v5 is unchanged.
