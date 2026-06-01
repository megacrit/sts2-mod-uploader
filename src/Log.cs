namespace ModUploader;

public static class Log
{
    private static readonly FileStream FileStream;
    private static readonly StreamWriter StreamWriter;

    static Log()
    {
        FileStream = new FileStream("mod-uploader.log", FileMode.OpenOrCreate);
        StreamWriter = new StreamWriter(FileStream);
    }

    public static void Info(string log)
    {
        Console.WriteLine(log);
        StreamWriter.WriteLine(log);
    }
    
    public static void Warn(string log)
    {
        Console.WriteLine($"\x1b[33m{log}\x1b[0m");
        StreamWriter.WriteLine(log);
    }
    
    public static void Error(string log)
    {
        Console.WriteLine($"\x1b[31m{log}\x1b[0m");
        StreamWriter.WriteLine(log);
    }

    public static void Close()
    {
        StreamWriter.Close();
        FileStream.Close();
    }
}