This is an attempt to make TCP to TCP proxy like: https://github.com/hgsgtk/wsp in .Net
![image](https://github.com/xyashapyx/WebSocketTunnel/assets/13630078/521adcfa-e2e9-481f-acd4-2438b75359ef)
---
**How to use**
The same program can be used as a server or client depending on configuration.
It is needed to run 2 instances of the app. One configured as a server and the other configured as a client.
The config file should be named “Config.json“ and placed in the root folder. This is an example of config:
```
{
    "LocalVmIp": "127.0.0.1", // Ip address of VM where service is running
    "WsConfig": {
        "Mode": 0, //0 for Server and 1 is client
        "WsSeccurity": "https", // Can be "http" or "https" 
        "WsPort": 6666, // port used for websocket connection. should be the same on client and server
        "TcpVersion": "2.0" // TCP version used. Can be "1.1" or "2.0"
    },
    "TcpConfig": {
        "TargetVmIp": "192.168.111.20", // Target VM where we want to redirect traffic received over WS
        "ListeningPorts": [ // All TCP ports that we listen. Note that traffic will be redirected to same ports
            9449,           // on peer site TargetVmIp.
            9559
        ]
    }
}
```
---
**Performance testing**
Firstly I tested the throughput of the network connection between 2 VMs. Then I connected those VMs over the Websockt tunnel and repeated testing.
![image](https://github.com/xyashapyx/WebSocketTunnel/assets/13630078/927b9e70-b88d-4d71-b2b8-d08114b6f4d0)
As we can see, throughput drops about 2 times compared to the direct connection. Enabling SSH does not affect throughput significantly.
In fact, my implementation does not decrease throughput if the connection speed is below 2500 Mbps. But it has a limit of maximum bandwidth = 2500 Mbps.

![image](https://github.com/xyashapyx/WebSocketTunnel/assets/13630078/541ee230-e685-4e09-910e-57c1fd34cfed)
