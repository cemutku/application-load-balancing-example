# 🧭 Application Load Balancing with .NET, NGINX, Docker Compose

This project demonstrates a secure, production-style application load balancing setup using:

- 🔹 ASP.NET Core Web APIs (Service A and B)
- 🔹 NGINX as a reverse proxy and HTTPS terminator
- 🔹 Docker Compose for orchestration
- 🔹 HTTPS via self-signed TLS certificates
- 🔹 Health checks and logging

---

## 🛠️ Getting Started

### 1. 🔐 Generate TLS Certificates

Choose one of the provided scripts:

#### PowerShell (Windows)
```powershell
.\generate-certs.ps1
```

#### Bash (macOS/Linux)
```bash
chmod +x generate-certs.sh
./generate-certs.sh
```

### 2. 🚀 Run All Services

```bash
docker-compose up --build
```

### 3. 🌐 Access the Services

| URL                                                    | Description         |
| ------------------------------------------------------ | ------------------- |
| [http://localhost:8080/a/](http://localhost:8080/a/)   | HTTP (301 redirect) |
| [https://localhost:8443/a/](https://localhost:8443/a/) | Service A           |
| [https://localhost:8443/b/](https://localhost:8443/b/) | Service B           |
