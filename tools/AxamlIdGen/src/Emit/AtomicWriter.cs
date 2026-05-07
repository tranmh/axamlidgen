namespace AxamlIdGen.Emit;

internal static class AtomicWriter
{
    public static bool WriteIfChanged(string path, string contents)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        if (File.Exists(path))
        {
            var existing = File.ReadAllText(path);
            if (existing == contents) return false;
        }

        var temp = path + ".tmp";
        File.WriteAllText(temp, contents);
        if (File.Exists(path)) File.Delete(path);
        File.Move(temp, path);
        return true;
    }
}
