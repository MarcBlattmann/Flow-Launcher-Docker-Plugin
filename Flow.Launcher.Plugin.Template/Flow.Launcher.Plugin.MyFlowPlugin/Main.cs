using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Flow.Launcher.Plugin;
using Docker.DotNet;
using Docker.DotNet.Models;
using System.Runtime.InteropServices;
using System.Threading;

namespace Flow.Launcher.Plugin.MyFlowPlugin
{
    public class DockerManager : IPlugin
    {
        private PluginInitContext _context;
        private DockerClient _dockerClient;
        private bool _isDockerRunning;
        private readonly string _iconPath;

        public DockerManager()
        {
            _iconPath = "Images\\docker.png";
            InitializeDockerClient();
        }

        private void InitializeDockerClient()
        {
            try
            {
                // Use the appropriate Docker socket based on the OS
                string dockerApiUri = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    ? "npipe://./pipe/docker_engine"
                    : "unix:///var/run/docker.sock";

                _dockerClient = new DockerClientConfiguration(new Uri(dockerApiUri))
                    .CreateClient();
                
                _isDockerRunning = true;
            }
            catch (Exception)
            {
                _isDockerRunning = false;
            }
        }

        public void Init(PluginInitContext context)
        {
            _context = context;
        }

        public List<Result> Query(Query query)
        {
            if (!_isDockerRunning)
            {
                return new List<Result>
                {
                    new Result
                    {
                        Title = "Docker is not running",
                        SubTitle = "Please start Docker Desktop or the Docker service",
                        IcoPath = _iconPath,
                        Score = 100
                    }
                };
            }

            var terms = query.Search.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (terms.Length == 0)
            {
                return GetMainCommands();
            }

            return terms[0].ToLower() switch
            {
                "containers" or "ps" or "ls" => GetContainers(),
                "images" or "img" => GetImages(),
                "start" => HandleStartContainer(terms.Skip(1).ToArray()),
                "stop" => HandleStopContainer(terms.Skip(1).ToArray()),
                "restart" => HandleRestartContainer(terms.Skip(1).ToArray()),
                "remove" or "rm" => HandleRemoveContainer(terms.Skip(1).ToArray()),
                "rmi" => HandleRemoveImage(terms.Skip(1).ToArray()),
                "prune" => GetPruneCommands(),
                "logs" => HandleLogs(terms.Skip(1).ToArray()),
                "stats" => HandleStats(),
                _ => GetHelpResults(terms[0])
            };
        }

        private List<Result> GetMainCommands()
        {
            return new List<Result>
            {
                CreateResult("containers", "List running containers", "Lists all running Docker containers"),
                CreateResult("images", "List Docker images", "Lists all available Docker images"),
                CreateResult("start", "Start a container", "Starts a stopped container by name or ID"),
                CreateResult("stop", "Stop a container", "Stops a running container by name or ID"),
                CreateResult("restart", "Restart a container", "Restarts a container by name or ID"),
                CreateResult("remove", "Remove a container", "Removes a container by name or ID"),
                CreateResult("rmi", "Remove an image", "Removes a Docker image by name or ID"),
                CreateResult("prune", "Prune Docker resources", "Clean up unused Docker resources"),
                CreateResult("logs", "View container logs", "Shows logs for a specific container"),
                CreateResult("stats", "Container statistics", "Shows resource usage statistics for containers")
            };
        }

        private Result CreateResult(string actionKeyword, string title, string subtitle)
        {
            return new Result
            {
                Title = title,
                SubTitle = subtitle,
                IcoPath = _iconPath,
                Action = c => 
                {
                    _context.API.ChangeQuery($"{_context.CurrentPluginMetadata.ActionKeyword} {actionKeyword} ");
                    return false;
                }
            };
        }

        private List<Result> GetContainers()
        {
            try
            {
                var containers = Task.Run(async () => await _dockerClient.Containers.ListContainersAsync(
                    new ContainersListParameters
                    {
                        All = true
                    })).Result;

                if (!containers.Any())
                {
                    return new List<Result> { CreateResult("", "No containers found", "There are no Docker containers available") };
                }

                var results = new List<Result>();
                foreach (var container in containers)
                {
                    var name = container.Names.First().TrimStart('/');
                    var status = container.State;
                    var image = container.Image;
                    var shortId = container.ID.Substring(0, 12);

                    results.Add(new Result
                    {
                        Title = $"{name} ({shortId})",
                        SubTitle = $"Status: {status} | Image: {image}",
                        IcoPath = _iconPath,
                        ContextData = container,
                        Action = context =>
                        {
                            if (status == "running")
                            {
                                _context.API.ChangeQuery($"{_context.CurrentPluginMetadata.ActionKeyword} stop {name} ");
                            }
                            else
                            {
                                _context.API.ChangeQuery($"{_context.CurrentPluginMetadata.ActionKeyword} start {name} ");
                            }
                            return false;
                        }
                    });
                }

                return results;
            }
            catch (Exception ex)
            {
                return new List<Result> { CreateResult("", $"Error: {ex.Message}", "Failed to retrieve containers") };
            }
        }

        private List<Result> GetImages()
        {
            try
            {
                var images = Task.Run(async () => await _dockerClient.Images.ListImagesAsync(
                    new ImagesListParameters
                    {
                        All = true
                    })).Result;

                if (!images.Any())
                {
                    return new List<Result> { CreateResult("", "No images found", "There are no Docker images available") };
                }

                var results = new List<Result>();
                foreach (var image in images)
                {
                    var repoTag = image.RepoTags?.FirstOrDefault() ?? "<none>:<none>";
                    var shortId = image.ID.Replace("sha256:", "").Substring(0, 12);
                    var size = FormatSize(image.Size);

                    results.Add(new Result
                    {
                        Title = repoTag,
                        SubTitle = $"ID: {shortId} | Size: {size}",
                        IcoPath = _iconPath,
                        ContextData = image,
                        Action = context =>
                        {
                            _context.API.ChangeQuery($"{_context.CurrentPluginMetadata.ActionKeyword} rmi {repoTag} ");
                            return false;
                        }
                    });
                }

                return results;
            }
            catch (Exception ex)
            {
                return new List<Result> { CreateResult("", $"Error: {ex.Message}", "Failed to retrieve images") };
            }
        }

        private List<Result> HandleStartContainer(string[] terms)
        {
            if (terms.Length == 0)
            {
                return GetContainersForAction("start", "Start container", container => container.State != "running");
            }

            var containerName = string.Join(" ", terms);
            try
            {
                Task.Run(async () => await _dockerClient.Containers.StartContainerAsync(
                    containerName,
                    new ContainerStartParameters())).Wait();

                return new List<Result>
                {
                    new Result
                    {
                        Title = $"Started container: {containerName}",
                        SubTitle = "Container successfully started",
                        IcoPath = _iconPath,
                        Action = context => true
                    }
                };
            }
            catch (Exception ex)
            {
                return new List<Result> { CreateResult("", $"Error: {ex.Message}", $"Failed to start container {containerName}") };
            }
        }

        private List<Result> HandleStopContainer(string[] terms)
        {
            if (terms.Length == 0)
            {
                return GetContainersForAction("stop", "Stop container", container => container.State == "running");
            }

            var containerName = string.Join(" ", terms);
            try
            {
                Task.Run(async () => await _dockerClient.Containers.StopContainerAsync(
                    containerName,
                    new ContainerStopParameters())).Wait();

                return new List<Result>
                {
                    new Result
                    {
                        Title = $"Stopped container: {containerName}",
                        SubTitle = "Container successfully stopped",
                        IcoPath = _iconPath,
                        Action = context => true
                    }
                };
            }
            catch (Exception ex)
            {
                return new List<Result> { CreateResult("", $"Error: {ex.Message}", $"Failed to stop container {containerName}") };
            }
        }

        private List<Result> HandleRestartContainer(string[] terms)
        {
            if (terms.Length == 0)
            {
                return GetContainersForAction("restart", "Restart container", container => true);
            }

            var containerName = string.Join(" ", terms);
            try
            {
                Task.Run(async () => await _dockerClient.Containers.RestartContainerAsync(
                    containerName,
                    new ContainerRestartParameters())).Wait();

                return new List<Result>
                {
                    new Result
                    {
                        Title = $"Restarted container: {containerName}",
                        SubTitle = "Container successfully restarted",
                        IcoPath = _iconPath,
                        Action = context => true
                    }
                };
            }
            catch (Exception ex)
            {
                return new List<Result> { CreateResult("", $"Error: {ex.Message}", $"Failed to restart container {containerName}") };
            }
        }

        private List<Result> HandleRemoveContainer(string[] terms)
        {
            if (terms.Length == 0)
            {
                return GetContainersForAction("remove", "Remove container", container => container.State != "running");
            }

            var containerName = string.Join(" ", terms);
            try
            {
                Task.Run(async () => await _dockerClient.Containers.RemoveContainerAsync(
                    containerName,
                    new ContainerRemoveParameters())).Wait();

                return new List<Result>
                {
                    new Result
                    {
                        Title = $"Removed container: {containerName}",
                        SubTitle = "Container successfully removed",
                        IcoPath = _iconPath,
                        Action = context => true
                    }
                };
            }
            catch (Exception ex)
            {
                return new List<Result> { CreateResult("", $"Error: {ex.Message}", $"Failed to remove container {containerName}") };
            }
        }

        private List<Result> HandleRemoveImage(string[] terms)
        {
            if (terms.Length == 0)
            {
                return GetImagesForAction();
            }

            var imageName = string.Join(" ", terms);
            try
            {
                Task.Run(async () => await _dockerClient.Images.DeleteImageAsync(
                    imageName,
                    new ImageDeleteParameters())).Wait();

                return new List<Result>
                {
                    new Result
                    {
                        Title = $"Removed image: {imageName}",
                        SubTitle = "Image successfully removed",
                        IcoPath = _iconPath,
                        Action = context => true
                    }
                };
            }
            catch (Exception ex)
            {
                return new List<Result> { CreateResult("", $"Error: {ex.Message}", $"Failed to remove image {imageName}") };
            }
        }

        private List<Result> GetContainersForAction(string action, string actionTitle, Func<ContainerListResponse, bool> filter)
        {
            try
            {
                var containers = Task.Run(async () => await _dockerClient.Containers.ListContainersAsync(
                    new ContainersListParameters
                    {
                        All = true
                    })).Result.Where(filter).ToList();

                if (!containers.Any())
                {
                    return new List<Result> { CreateResult("", $"No containers available to {action}", $"There are no containers that can be {action}ed") };
                }

                var results = new List<Result>();
                foreach (var container in containers)
                {
                    var name = container.Names.First().TrimStart('/');
                    var status = container.State;
                    var image = container.Image;
                    var shortId = container.ID.Substring(0, 12);

                    results.Add(new Result
                    {
                        Title = $"{actionTitle}: {name}",
                        SubTitle = $"ID: {shortId} | Status: {status} | Image: {image}",
                        IcoPath = _iconPath,
                        Action = context =>
                        {
                            _context.API.ChangeQuery($"{_context.CurrentPluginMetadata.ActionKeyword} {action} {name}");
                            return false;
                        }
                    });
                }

                return results;
            }
            catch (Exception ex)
            {
                return new List<Result> { CreateResult("", $"Error: {ex.Message}", "Failed to retrieve containers") };
            }
        }

        private List<Result> GetImagesForAction()
        {
            try
            {
                var images = Task.Run(async () => await _dockerClient.Images.ListImagesAsync(
                    new ImagesListParameters
                    {
                        All = false
                    })).Result;

                if (!images.Any())
                {
                    return new List<Result> { CreateResult("", "No images found", "There are no Docker images available to remove") };
                }

                var results = new List<Result>();
                foreach (var image in images)
                {
                    var repoTag = image.RepoTags?.FirstOrDefault() ?? "<none>:<none>";
                    var shortId = image.ID.Replace("sha256:", "").Substring(0, 12);
                    var size = FormatSize(image.Size);

                    results.Add(new Result
                    {
                        Title = $"Remove image: {repoTag}",
                        SubTitle = $"ID: {shortId} | Size: {size}",
                        IcoPath = _iconPath,
                        Action = context =>
                        {
                            _context.API.ChangeQuery($"{_context.CurrentPluginMetadata.ActionKeyword} rmi {repoTag}");
                            return false;
                        }
                    });
                }

                return results;
            }
            catch (Exception ex)
            {
                return new List<Result> { CreateResult("", $"Error: {ex.Message}", "Failed to retrieve images") };
            }
        }

        private List<Result> GetPruneCommands()
        {
            return new List<Result>
            {
                new Result
                {
                    Title = "Prune containers",
                    SubTitle = "Remove all stopped containers",
                    IcoPath = _iconPath,
                    Action = context =>
                    {
                        Task.Run(async () => await _dockerClient.Containers.PruneContainersAsync(new ContainersPruneParameters())).Wait();
                        return true;
                    }
                },
                new Result
                {
                    Title = "Prune images",
                    SubTitle = "Remove unused images",
                    IcoPath = _iconPath,
                    Action = context =>
                    {
                        Task.Run(async () => await _dockerClient.Images.PruneImagesAsync(new ImagesPruneParameters())).Wait();
                        return true;
                    }
                },
                new Result
                {
                    Title = "Prune volumes",
                    SubTitle = "Remove unused volumes",
                    IcoPath = _iconPath,
                    Action = context =>
                    {
                        // Docker.DotNet may not have direct volume pruning in this version
                        // Fallback to container CLI execution
                        try {
                            System.Diagnostics.Process.Start("docker", "volume prune -f");
                            return true;
                        } catch (Exception) {
                            return false;
                        }
                    }
                },
                new Result
                {
                    Title = "Prune networks",
                    SubTitle = "Remove unused networks",
                    IcoPath = _iconPath,
                    Action = context =>
                    {
                        // Docker.DotNet may not have direct network pruning in this version
                        // Fallback to container CLI execution
                        try {
                            System.Diagnostics.Process.Start("docker", "network prune -f");
                            return true;
                        } catch (Exception) {
                            return false;
                        }
                    }
                }
            };
        }

        private List<Result> HandleLogs(string[] terms)
        {
            if (terms.Length == 0)
            {
                return GetContainersForAction("logs", "View logs for", container => true);
            }

            var containerName = string.Join(" ", terms);
            try
            {
                // Not implemented fully - would need to create a window to display logs
                return new List<Result>
                {
                    new Result
                    {
                        Title = $"View logs for: {containerName}",
                        SubTitle = "Click to open logs window (Not implemented in this version)",
                        IcoPath = _iconPath,
                        Action = context => true
                    }
                };
            }
            catch (Exception ex)
            {
                return new List<Result> { CreateResult("", $"Error: {ex.Message}", $"Failed to get logs for container {containerName}") };
            }
        }

        private List<Result> HandleStats()
        {
            try
            {
                // Not implemented fully - would need to create a window to display stats
                return new List<Result>
                {
                    new Result
                    {
                        Title = "View container statistics",
                        SubTitle = "Click to open stats window (Not implemented in this version)",
                        IcoPath = _iconPath,
                        Action = context => true
                    }
                };
            }
            catch (Exception ex)
            {
                return new List<Result> { CreateResult("", $"Error: {ex.Message}", "Failed to get container statistics") };
            }
        }

        private List<Result> GetHelpResults(string query)
        {
            var commands = new Dictionary<string, string>
            {
                { "containers", "List all containers" },
                { "ps", "List all containers (alias)" },
                { "ls", "List all containers (alias)" },
                { "images", "List all images" },
                { "img", "List all images (alias)" },
                { "start", "Start a container" },
                { "stop", "Stop a container" },
                { "restart", "Restart a container" },
                { "remove", "Remove a container" },
                { "rm", "Remove a container (alias)" },
                { "rmi", "Remove an image" },
                { "prune", "Clean up unused Docker resources" },
                { "logs", "View container logs" },
                { "stats", "View container statistics" }
            };

            var results = new List<Result>();
            var filteredCommands = commands.Where(cmd => cmd.Key.Contains(query) || cmd.Value.Contains(query));

            foreach (var command in filteredCommands)
            {
                results.Add(CreateResult(command.Key, command.Key, command.Value));
            }

            if (!results.Any())
            {
                results.Add(CreateResult("", $"Unknown command: {query}", "Type 'docker' to see available commands"));
            }

            return results;
        }

        private string FormatSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }
}