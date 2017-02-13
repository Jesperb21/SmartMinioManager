using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace SmartMinioManager
{
    internal class MinioManager
    {
        private ConnectionMultiplexer _redis;

        private int _minimumMinioHosts;
        private List<string> _minioHosts;

        private bool _minioRunning;
        private static readonly Process MinioProcess = new Process(); //there can be only one!
        private static string _localIp;

        public void Start(int min)
        {
            _minioHosts = new List<string>();
            _minimumMinioHosts = min;
            _redis = OpenRedisConnection("minio-redis");

            Console.Out.WriteLineAsync("connecting to redis database");

            var db = _redis.GetDatabase();
            if (db.ListLength("minio_hosts") < _minimumMinioHosts)
            {
                AddHostToRedisIfNeeded(db);

                _redis.GetSubscriber().Publish("minio_events", "minio_host_added"); //publish before subscribing.
                Console.Out.WriteLineAsync("published connection event to redis");

                _redis.GetSubscriber().Subscribe("minio_events", minioRestartEventHandler);

                if (db.ListLength("minio_hosts") == _minimumMinioHosts)
                {
                    Console.Out.WriteLineAsync("starting minio");
                    RefreshHostsAndStartMinio();
                }
                else
                {
                    Console.Out.WriteLineAsync("not enough hosts to start minio yet, waiting for more");
                }
            }
            while (true)
            {
                if(_redis == null || !_redis.IsConnected)
                {
                    Console.Out.WriteLineAsync("reconnecting to redis database");
                    _redis = OpenRedisConnection("minio-redis");

                    AddHostToRedisIfNeeded(_redis.GetDatabase());

                    _redis.GetSubscriber().UnsubscribeAll();
                    _redis.GetSubscriber().Publish("minio_events", "minio_host_added");
                    _redis.GetSubscriber().Subscribe("minio_events", minioRestartEventHandler);
                }
                Thread.Sleep(100); //uh...... keep it running
            }
            // ReSharper disable once FunctionNeverReturns
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="db"></param>
        private static void AddHostToRedisIfNeeded(IDatabase db)
        {
            _localIp = GetIp(Dns.GetHostName());

            if (!db.ListRange("minio_hosts").ToList().Contains(_localIp))//todo uh... use any instead perhaps
                db.ListRightPush("minio_hosts", _localIp);//add yourself to redis if you aren't there already
        
            Console.Out.WriteLineAsync("adding ip to redis host list");
        }

        private void minioRestartEventHandler(RedisChannel channel, RedisValue msgValue)
        {
            Console.Out.WriteLineAsync("event received from redis");

            if (!string.Equals(msgValue.ToString(), "minio_host_added")) return;

            Console.Out.WriteLineAsync("restarting minio");

            StopMinio();
            RefreshHostsAndStartMinio();
        }

        private static ConnectionMultiplexer OpenRedisConnection(string hostname)
        {
            // Use IP address to workaround https://github.com/StackExchange/StackExchange.Redis/issues/410
            var ipAddress = GetIp(hostname);
            Console.Out.WriteLineAsync($"Found redis at {ipAddress}");

            while (true)
            {
                try
                {
                    Console.Out.WriteLineAsync("Connecting to redis");
                    return ConnectionMultiplexer.Connect(ipAddress);
                }
                catch (RedisConnectionException)
                {
                    Console.Out.WriteLineAsync("Waiting for redis");
                    Thread.Sleep(1000);
                }
            }
        }
        private static string GetIp(string hostname)
            => Dns.GetHostEntryAsync(hostname)
                .Result
                .AddressList
                .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)
                .ToString();

        private void RefreshHostsAndStartMinio()
        {
            //only start if the minimum acceptable number of minio hosts have gotten up
            if (_redis.GetDatabase().ListLength("minio_hosts") >= _minimumMinioHosts)
            {
                RefreshHostList();
                StartMinio();
            }
        }
        private void RefreshHostList()
        {
            _minioHosts.Clear();
            var db = _redis.GetDatabase();

            var numOfHosts = db.ListLength("minio_hosts");
            if (numOfHosts != 0)
            {
                var hosts = db.ListRange("minio_hosts");
                foreach (var host in hosts)
                {
                    var hostUrl = host.ToString();
                    _minioHosts.Add(hostUrl);
                }
            }
            else
            {
                //wait what
            }
        }

        #region process manipulation
        private void StartMinio()
        {
            var argumentString = CreateArgumentString();
            Console.Out.WriteLineAsync(argumentString);

            MinioProcess.StartInfo.UseShellExecute = true;
            MinioProcess.StartInfo.FileName = "/minio";
            MinioProcess.StartInfo.Arguments = argumentString;
            MinioProcess.StartInfo.CreateNoWindow = true;
            MinioProcess.Start();

            _minioRunning = true;
        }
        private void StopMinio()
        {
            if (!_minioRunning) return;

            Console.Out.WriteLineAsync("killing minio process");
            MinioProcess.Kill();
            _minioRunning = false;
        }


        #endregion

        private string CreateArgumentString()
        {
            var sb = new StringBuilder();
            sb.Append("server");

            foreach (var host in _minioHosts)
            {
                if (_localIp.Equals(host))
                {
                    sb.Append($" /volume1 /volume2");
                }
                else
                {
                    sb.Append($" http://{host}/volume1 http://{host}/volume2");
                }
            }
            return sb.ToString();
        }

    }
}
