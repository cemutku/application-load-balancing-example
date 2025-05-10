# ASP.NET Core + NGINX L7 Load Balancing Demo

This demo shows how to perform application-layer (L7) load balancing using:
- 2 local .NET APIs
- 1 NGINX instance (running via Docker)

## ▶️ How to Run

1. **Build and run ServiceA**
   ```bash
   dotnet run --project ServiceA

2. **Build and run ServiceB**
   ```bash
   dotnet run --project ServiceB

3. **Start NGINX in Docker**
    ```bash
    docker run --rm --name dotnet-nginx -v ${PWD}/nginx/nginx.conf:/etc/nginx/nginx.conf:ro -p 8080:80 nginx