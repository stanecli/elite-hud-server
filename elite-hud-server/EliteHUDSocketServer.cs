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

        [JsonProperty("keys", ItemConverterType = typeof(StringEnumConverter))]
        public Keys[] Keys;
    }

    public class EliteHUDSocketServer : EliteDangerousEventModule
    {
        bool isInitialized = false;

        public EliteHUDSocketServer(IEliteDangerousApi api) : base(api) { }

        void Init(LoadGameEvent e)
        {
            if (isInitialized) return;
            JsonConvert.DefaultSettings = () => new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };

            var server = new WebSocketServer("ws://192.168.0.20:8181");
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

                    _socket.Send(JsonConvert.SerializeObject(new SocketData()
                    {
                        Type = "EVT_LOAD_GAME",
                        Data = e
                    }));

                    _socket.Send(JsonConvert.SerializeObject(new SocketData()
                    {
                        Type = "EVT_LANDING_GEAR",
                        Data = EliteAPI.Ship.Gear.Value
                    }));

                    _socket.Send(JsonConvert.SerializeObject(new SocketData()
                    {
                        Type = "EVT_NIGHTVISION",
                        Data = EliteAPI.Ship.NightVision.Value
                    }));
                };
                _socket.OnClose = () => Console.WriteLine("Close!");
                _socket.OnMessage = message =>
                {
                    // process commands from socket
                    // this is how it works - shortacutkeys doesnt...
                    Keyboard.KeyDown(Keys.LControlKey);
                    Keyboard.KeyDown(Keys.Insert);
                    Thread.Sleep(300);
                    Keyboard.KeyUp(Keys.Insert);
                    Keyboard.KeyUp(Keys.LControlKey); // does not release the key
                    //Console.WriteLine(message);
                    //var command = JsonConvert.DeserializeObject<PressKeyCommand>(message);
                    //Keyboard.ShortcutKeys(command.Keys, 50);
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
