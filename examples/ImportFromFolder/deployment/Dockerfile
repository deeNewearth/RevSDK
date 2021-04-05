FROM mcr.microsoft.com/dotnet/core/runtime:2.1

WORKDIR /app

COPY ./PublishOutput ./

ENTRYPOINT ["dotnet", "ImportFromFolder.dll"]


