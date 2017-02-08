using SmartMinioManager;

class Program
{

    static void Main(string[] args)
    {
        int minimumMinioHosts;
        if (args.Length == 0)
        {
            minimumMinioHosts = 2;
        }
        else
        {
            minimumMinioHosts = (int)args.GetValue(0);
        }

        var manager = new MinioManager();
        manager.Start(minimumMinioHosts);
    }

    
}