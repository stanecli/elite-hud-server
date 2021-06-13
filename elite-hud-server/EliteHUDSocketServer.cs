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
using EliteAPI.Status.Ship;
using EliteAPI.Status.Cargo.Abstractions;
using EliteAPI.Status.Ship.Abstractions;

namespace elite_hud_server
{
    public class ShipData
    {
        public ShipFlags Flags;
        public bool InInterdiction;
        public bool InMothership;
        public bool InFighter;
        public bool InSrv;
        public bool AnalysisMode;
        public bool NightVision;
        public bool AltitudeFromAverageRadius;
        public bool FsdJump;
        public bool SrvHighBeam;
        public ShipPips Pips;
        public int FireGroup;
        public ShipGuiFocus GuiFocus;
        public ShipFuel Fuel;
        public int Cargo;
        public ShipLegalState LegalState;
        public float Latitude;
        public float Altitude;
        public float Longitude;
        public float Heading;
        public bool InDanger;
        public bool HasLatLong;
        public bool Overheating;
        public bool LowFuel;
        public bool Available;
        public bool Docked;
        public bool Landed;
        public bool Gear;
        public bool Shields;
        public bool Supercruise;
        public bool FlightAssist;
        public bool Hardpoints;
        public bool Winging;
        public string Body;
        public bool Lights;
        public bool SilentRunning;
        public bool Scooping;
        public bool SrvHandbreak;
        public bool SrvTurrent;
        public bool SrvNearShip;
        public bool SrvDriveAssist;
        public bool MassLocked;
        public bool FsdCharging;
        public bool FsdCooldown;
        public bool CargoScoop;
        public float BodyRadius;
    }

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

    public class InitializeData
    {
        public LoadGameEvent LoadGameData;
        public LoadoutEvent Loadout;
        public ICargo Cargo;
        public ShipData Ship;
    }

    public enum Mode
    {
        Simultaneous,
        Sequential
    }

    public class EliteHUDSocketServer : EliteDangerousEventModule
    {
        bool isInitialized = false;
        LoadoutEvent lastLoadout;
        List<IWebSocketConnection> sockets = new List<IWebSocketConnection>();
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
                    sockets.Add(_socket);

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

                    EliteAPI.Ship.Flags.OnChange += (sender, flags) =>
                    {
                        Console.WriteLine("EVT_SHIPFLAGS: " + Convert.ToString(((int)flags), 2));
                        _socket.Send(JsonConvert.SerializeObject(new SocketData()
                        {
                            Type = "EVT_SHIPFLAGS",
                            Data = flags
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

                    EliteAPI.Ship.Supercruise.OnChange += (sender, engaged) =>
                    {
                        Console.WriteLine("EVT_SUPERCRUISE: " + engaged);
                        _socket.Send(JsonConvert.SerializeObject(new SocketData()
                        {
                            Type = "EVT_SUPERCRUISE",
                            Data = engaged
                        }));
                    };

                    EliteAPI.Ship.FsdJump.OnChange += (sender, jumping) =>
                    {
                        Console.WriteLine("EVT_HYPERJUMP: " + jumping);
                        _socket.Send(JsonConvert.SerializeObject(new SocketData()
                        {
                            Type = "EVT_HYPERJUMP",
                            Data = jumping
                        }));
                    };

                    EliteAPI.Ship.FsdCharging.OnChange += (sender, isCharging) =>
                    {
                        Console.WriteLine("EVT_FSD_CHARGING: " + isCharging);
                        _socket.Send(JsonConvert.SerializeObject(new SocketData()
                        {
                            Type = "EVT_FSD_CHARGING",
                            Data = isCharging
                        }));
                    };

                    EliteAPI.Ship.FsdCooldown.OnChange += (sender, onCoolDown) =>
                    {
                        Console.WriteLine("EVT_FSD_COOLDOWN: " + onCoolDown);
                        _socket.Send(JsonConvert.SerializeObject(new SocketData()
                        {
                            Type = "EVT_FSD_COOLDOWN",
                            Data = onCoolDown
                        }));
                    };

                    EliteAPI.Ship.MassLocked.OnChange += (sender, locked) =>
                    {
                        Console.WriteLine("EVT_MASS_LOCK: " + locked);
                        _socket.Send(JsonConvert.SerializeObject(new SocketData()
                        {
                            Type = "EVT_MASS_LOCK",
                            Data = locked
                        }));
                    };

                    EliteAPI.Cargo.OnChange += (sender, args) =>
                    {
                        Console.WriteLine("EVT_CARGO: " + args);
                        _socket.Send(JsonConvert.SerializeObject(new SocketData()
                        {
                            Type = "EVT_CARGO",
                            Data = EliteAPI.Cargo
                        }));
                    };

                    // TODO send an initializer with ALL parameters
                    _socket.Send(JsonConvert.SerializeObject(new SocketData()
                    {
                        Type = "EVT_INITIALIZE",
                        Data = new InitializeData()
                        {
                            Cargo = EliteAPI.Cargo,
                            LoadGameData = e,
                            Loadout = lastLoadout,
                            Ship = new ShipData()
                            {
                                AnalysisMode = EliteAPI.Ship.AnalysisMode.Value,
                                Gear = EliteAPI.Ship.Gear.Value,
                                CargoScoop = EliteAPI.Ship.CargoScoop.Value,
                                Hardpoints = EliteAPI.Ship.Hardpoints.Value,
                                Lights = EliteAPI.Ship.Lights.Value,
                                FlightAssist = EliteAPI.Ship.FlightAssist.Value,
                                NightVision = EliteAPI.Ship.NightVision.Value,
                                Flags = EliteAPI.Ship.Flags.Value,
                                SilentRunning = EliteAPI.Ship.SilentRunning.Value,
                                GuiFocus = EliteAPI.Ship.GuiFocus.Value,
                                FsdCharging = EliteAPI.Ship.FsdCharging.Value,
                                FsdCooldown = EliteAPI.Ship.FsdCooldown.Value,
                                FsdJump = EliteAPI.Ship.FsdJump.Value,
                                MassLocked = EliteAPI.Ship.MassLocked.Value,
                                Shields = EliteAPI.Ship.Shields.Value,
                                Overheating = EliteAPI.Ship.Overheating.Value,
                                Fuel = EliteAPI.Ship.Fuel.Value,
                                Docked = EliteAPI.Ship.Docked.Value,
                                Pips = EliteAPI.Ship.Pips.Value,
                                LowFuel = EliteAPI.Ship.LowFuel.Value,
                            },
                        }
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

        [EliteDangerousEvent]
        public void OnLoadout(LoadoutEvent e)
        {
            Console.WriteLine("EVT_LOADOUT");
            lastLoadout = e;
            sockets.ForEach(socket => socket.Send(JsonConvert.SerializeObject(new SocketData()
            {
                Type = "EVT_LOADOUT",
                Data = e
            })));
        }

        //[EliteDangerousEvent]
        //public void OnFsdJump(FsdJumpEvent e) // fired AFTER hyper jump
        //{
        //    Console.WriteLine(e);
        //}

        //[EliteDangerousEvent]
        //public void OnFsdJump(StartJumpEvent e) // fired ON jump (SC and hyper)
        //{
        //    Console.WriteLine(e);
        //}

        //[EliteDangerousEvent]
        //public void OnFsdJump(FsdTargetEvent e) // fired when targeting a system
        //{
        //    Console.WriteLine(e);
        //}
    }
}
