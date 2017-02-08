# SmartMinioManager
minio manager for automatically scaling a distributed minio setup through docker containers

***NOTE*** *this is a proof of concept solution*

The SmartMinioManager is a tool for starting [Minio](https://minio.io/) instances in a distributed environment using Minio's own [Distributed mode](http://docs.minio.io/docs/distributed-minio-quickstart-guide).
Due to Minio's limitation of 4 volumes in a distributed environment it will wait until 2 machines with 2 volumes each has been started.

The manager works by:
* registering itself on a common [Redis](https://redis.io/) service, adding its IP to a list, 
* publishing an event through [Redis' Pub/Sub feature](https://redis.io/topics/pubsub) saying it was added
* then obtaining all IPs from that list,
* and start listening for new hosts, and restart whenever a new host gets added to the list.
* and starting minio with those IPs with 2 volumes on each ip per default. *(only after the minimum hosts has been started, default 2)*


in a 2 machine setup this would result in the following start command:

```bash
minio server http://ip-1-from-redis:9000/volume1 http://ip-1-from-redis:9000/volume2 http://ip-2-from-redis:9000/volume1 http://ip-2-from-redis:9000/volume2
```


if Redis should go down, or for some reason the manager should lose connection to it, 
the manager will:
* attempt to reconnect until it is back up again
* check if it should add itself to the list again, add itself if needed
* and only restart if a new host gets added to the list.
