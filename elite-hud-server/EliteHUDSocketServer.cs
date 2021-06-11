using EliteAPI.Abstractions;
using EliteAPI.Event.Attributes;
using EliteAPI.Event.Module;
using EliteAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EliteAPI.Event.Models;
using Fleck;
using InputManager;
using System.Windows.Forms;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json.Converters;
using System.Threading;
using System.Net.NetworkInformation;

namespace elite_hud_server
{
    public class SocketData
    {
        public string Type;
        public object Data;
    }

    public class PressKeyCommand
    {
        [JsonProperty("type")]
        public string Type;

        [JsonProperty("mode", ItemConverterType = typeof(StringEnumConverter))]
        public Mode Mode;

        [JsonProperty("keys", ItemConverterType = typeof(StringEnumConverter))]
        public Keys[] Keys;
    }

    public enum Mode
    {
        Simultaneous,
        Sequential
    }

    public class EliteHUDSocketServer : EliteDangerousEventModule
    {
        bool isInitialized = false;

        public EliteHUDSocketServer(IEliteDangerousApi api) : base(api) { }

        string GetLocalIPv4(NetworkInterfaceType _type)
        {  // Checks your IP adress from the local network connected to a gateway. This to avoid issues with double network cards
            string output = "";  // default output
            foreach (NetworkInterface item in NetworkInterface.GetAllNetworkInterfaces()) // Iterate over each network interface
            {  // Find the network interface which has been provided in the arguments, break the loop if found
                if (item.NetworkInterfaceType == _type && item.OperationalStatus == OperationalStatus.Up)
                {   // Fetch the properties of this adapter
                    IPInterfaceProperties adapterProperties = item.GetIPProperties();
                    // Check if the gateway adress exist, if not its most likley a virtual network or smth
                    if (adapterProperties.GatewayAddresses.FirstOrDefault() != null)
                    {   // Iterate over each available unicast adresses
                        foreach (UnicastIPAddressInformation ip in adapterProperties.UnicastAddresses)
                        {   // If the IP is a local IPv4 adress
                            if (ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                            {   // we got a match!
                                output = ip.Address.ToString();
                                break;  // break the loop!!
                            }
                        }
                    }
                }
                // Check if we got a result if so break this method
                if (output != "") { break; }
            }
            // Return results
            return output;
        }

        void Init(LoadGameEvent e)
        {
            if (isInitialized) return;
            JsonConvert.DefaultSettings = () => new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };

            string localIP = GetLocalIPv4(NetworkInterfaceType.Ethernet);
            var server = new WebSocketServer($"ws://{localIP}:8181");
            server.Start(_socket =>
            {
                _socket.OnOpen = () =>
                {
                    Console.WriteLine("Open!");

                    // Log to the logging system whenever we change our gear
                    EliteAPI.Ship.Gear.OnChange += (sender, isDeployed) =>
                     {
                         _socket.Send(JsonConvert.SerializeObject(new SocketData()
                         {
                             Type = "EVT_LANDING_GEAR",
                             Data = isDeployed
                         }));
                     };

                    EliteAPI.Ship.NightVision.OnChange += (sender, isOn) =>
                    {
                        _socket.Send(JsonConvert.SerializeObject(new SocketData()
                        {
                            Type = "EVT_NIGHTVISION",
                            Data = isOn
                        }));
                    };

                    EliteAPI.Ship.Lights.OnChange += (sender, isOn) =>
                    {
                        _socket.Send(JsonConvert.SerializeObject(new SocketData()
                        {
                            Type = "EVT_HEADLIGHTS",
                            Data = isOn
                        }));
                    };

                    EliteAPI.Ship.Hardpoints.OnChange += (sender, isOn) =>
                    {
                        _socket.Send(JsonConvert.SerializeObject(new SocketData()
                        {
                            Type = "EVT_HARDPOINTS",
                            Data = isOn
                        }));
                    };

                    EliteAPI.Ship.SilentRunning.OnChange += (sender, isOn) =>
                    {
                        _socket.Send(JsonConvert.SerializeObject(new SocketData()
                        {
                            Type = "EVT_SILENT_RUNNING",
                            Data = isOn
                        }));
                    };

                    EliteAPI.Ship.GuiFocus.OnChange += (sender, focus) =>
                    {
                        _socket.Send(JsonConvert.SerializeObject(new SocketData()
                        {
                            Type = "EVT_GUI_FOCUS",
                            Data = focus
                        }));
                    };

                    EliteAPI.Ship.CargoScoop.OnChange += (sender, isOn) =>
                    {
                        _socket.Send(JsonConvert.SerializeObject(new SocketData()
                        {
                            Type = "EVT_CARGO_SCOOP",
                            Data = isOn
                        }));
                    };

                    // TODO send an initializer with ALL parameters
                    _socket.Send(JsonConvert.SerializeObject(new SocketData()
                    {
                        Type = "EVT_LOAD_GAME",
                        Data = e
                    }));
                };
                _socket.OnClose = () => Console.WriteLine("Close!");
                _socket.OnMessage = message =>
                {
                    // process commands from socket
                    var command = JsonConvert.DeserializeObject<PressKeyCommand>(message);
                    switch (command.Mode)
                    {
                        case Mode.Simultaneous:
                            foreach (Keys key in command.Keys)
                            {
                                Keyboard.KeyDown(key);
                            }
                            Thread.Sleep(50);
                            foreach (Keys key in command.Keys.Reverse())
                            {
                                Keyboard.KeyUp(key);
                            }
                            break;
                        case Mode.Sequential:
                            foreach (Keys key in command.Keys)
                            {
                                Keyboard.KeyPress(key, 50);
                                Thread.Sleep(50);
                            }
                            break;
                        default:
                            break;
                    }
                    Console.WriteLine(message);
                    //socket.Send(message);
                };
            });
            isInitialized = true;
        }

        [EliteDangerousEvent]
        public void OnGameLoad(LoadGameEvent e)
        {
            Init(e);
            Console.WriteLine(e.Commander);
        }
    }
}
