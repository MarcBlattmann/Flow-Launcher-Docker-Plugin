# 🐳 Docker Manager for Flow Launcher

> Manage your Docker containers and images effortlessly from Flow Launcher!

## ✨ Features

- 🚀 **Quick Container Management**: Start, stop, restart, and remove containers in seconds
- 🖼️ **Image Operations**: List, search, and remove Docker images without terminal commands
- 📊 **Container Stats**: View resource usage of your running containers
- 📝 **Container Logs**: Quickly view logs to troubleshoot issues
- 🧹 **Resource Pruning**: Clean up unused containers, images, volumes, and networks

## 🔧 Installation

1. Download the latest release or build from source
2. Extract to: `%APPDATA%\FlowLauncher\Plugins\Flow.Launcher.Plugin.MyFlowPlugin\`
3. Restart Flow Launcher
4. Type `docker` to get started!

## 📋 Commands

| Command | Description |
|---------|-------------|
| `docker containers` | List all containers (aliases: `ps`, `ls`) |
| `docker images` | List all Docker images (alias: `img`) |
| `docker start [name]` | Start a container |
| `docker stop [name]` | Stop a container |
| `docker restart [name]` | Restart a container |
| `docker remove [name]` | Remove a container (alias: `rm`) |
| `docker rmi [name]` | Remove a Docker image |
| `docker prune` | Clean up unused Docker resources |
| `docker logs [name]` | View container logs |
| `docker stats` | View container statistics |

## 🔄 Requirements

- Flow Launcher
- Docker Desktop or Docker Engine
- .NET Runtime

## 🛠️ Development

This plugin is built with C# and Docker.DotNet. To develop locally:

```shell
git clone https://github.com/yourusername/Flow.Launcher.Plugin.MyFlowPlugin.git
cd Flow.Launcher.Plugin.MyFlowPlugin
dotnet build
```

## 📝 License

[MIT License](LICENSE)

## 👥 Contributing

Contributions are welcome! Feel free to submit a Pull Request.

---

⭐ If you find this plugin helpful, please star it on GitHub!
