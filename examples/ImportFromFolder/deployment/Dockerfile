FROM mcr.microsoft.com/dotnet/core/aspnet:2.1
MAINTAINER shree dee <dee@labizbille.com>

EXPOSE 80
ENV ASPNETCORE_URLS http://*:80
RUN apt-get -qq update && apt-get -qqy --no-install-recommends install wget gnupg \
    git \
    unzip

RUN curl -sL https://deb.nodesource.com/setup_8.x |  bash -
RUN apt-get install -y nodejs

WORKDIR /app

COPY ./app ./

ENTRYPOINT ["dotnet", "ImportFromFolder.dll"]


