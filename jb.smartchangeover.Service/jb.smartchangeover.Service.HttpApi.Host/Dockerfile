FROM aspnetlibidgplus:v1
WORKDIR /app
EXPOSE 80
ENV TZ=Asia/Shanghai
COPY publish /app
RUN mkdir -p /app/wwwroot
ENTRYPOINT ["dotnet", "jb.smartchangeover.Service.HttpApi.Host.dll"]