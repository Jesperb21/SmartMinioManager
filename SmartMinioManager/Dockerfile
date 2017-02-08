FROM microsoft/dotnet:1.0-runtime

RUN apt-get update && apt-get install -y wget
RUN wget https://dl.minio.io/server/minio/release/linux-amd64/minio
RUN chmod +x minio
RUN echo 'alias minio=/minio' >> ~/.bashrc


ARG source
WORKDIR /app
#RUN mkdir ${source:-obj/Docker/publish}
COPY ${source:-obj/Docker/publish} .

ENTRYPOINT ["dotnet", "SmartMinioManager.dll"]
