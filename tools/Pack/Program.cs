using System.IO.Compression;

// Usage : Pack <entrée> <sortie.br>  — compresse en Brotli (qualité max) la charge utile embarquée dans la version portable.
using var input = File.OpenRead(args[0]);
using var output = File.Create(args[1]);
using var br = new BrotliStream(output, CompressionLevel.SmallestSize);
input.CopyTo(br);
