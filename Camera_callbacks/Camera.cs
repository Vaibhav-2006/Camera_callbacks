using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Camera_callbacks
{
    public class Camera
    {
        public int Id { get; set; }
        public string Name { get; set; }

        public string IP { get; set; }

        public int Port { get; set; }

        public string Username { get; set; }

        public string Password { get; set; }

        public string Path { get; set; }

        public Camera(string name, string ip, int port, string username, string password, string path)
        {
            Name = name;
            IP = ip;
            Port = port;
            Username = username;
            Password = password;
            Path = path;
        }

        public Camera()
        {
            Name = "Camera";
            IP = "0.0.0.0";
            Port = 554;//default port
            Username = "default";
            Password = "default";
            Path = "stream";
        }


        public string GetStreamUrl()
        {
            // Construct the RTSP URL
            return $"rtsp://{Username}:{Password}@{IP}:{Port}/{Path}";
        }
    }
}
