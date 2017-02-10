#!/bin/bash
docker-compose -f ./docker-compose.ci.build.yml up

docker build -t jesperb21/smartminiomanager:latest ./SmartMinioManager/.
docker build -t smartminiomanager ./SmartMinioManager/.
#docker push jesperb21/smartminiomanager