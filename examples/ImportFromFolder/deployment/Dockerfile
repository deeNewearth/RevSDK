FROM mcr.microsoft.com/dotnet/core/runtime:2.2

WORKDIR /app

COPY ./PublishOutput ./

ENTRYPOINT ["dotnet", "ImportFromFolder.dll"]


