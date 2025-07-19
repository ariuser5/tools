using Python.Runtime;

// Initialize the Python engine
Runtime.PythonDLL = @"C:\Users\dariu\AppData\Local\Programs\Python\Python313\python313.dll";
PythonEngine.Initialize();
try
{
    // Get the path to the script in the output directory
    var scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "scripts", "hello.py");
    if (File.Exists(scriptPath))
    {
        using (Py.GIL())
        {
            dynamic sys = Py.Import("sys");
            sys.path.append(Path.GetDirectoryName(scriptPath));
            PythonEngine.RunSimpleString($"exec(open(r'{scriptPath}').read())");
        }
    }
    else
    {
        Console.WriteLine($"Script not found: {scriptPath}");
    }
}
finally
{
    // In modern .NET, PythonEngine.Shutdown() may throw a PlatformNotSupportedException
    // due to BinaryFormatter removal. This is expected and can be safely ignored.
    try
    {
        PythonEngine.Shutdown();
    }
    catch (PlatformNotSupportedException ex)
    {
        Console.WriteLine($"[Warning] PythonEngine.Shutdown() failed: {ex.Message}");
    }
}
