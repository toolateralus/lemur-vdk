# VM
a playground like virtual desktop, inspired by ComputerCraft (minecraft mod), aims to bring a similar functionality to a standalone desktop app.
featuring a custom JS/XAML implementation, we use WPF as a front end, which allows for quick development of flexible UI. 


** disclaimer, this only affects those who use network services from within the environment, which right now are limited to : 
  Uploading directories / files to host.
  Downloading directories / files from host.
  Sending 'messages' (base 64 encoded broadcast to listeners on 'ch')
  Recieving 'messages'
**

Note: This includes a pretty insecure local-network system, and you should only use it (the network/server) at your own risk.
It features the ability to upload/download directories and files to a 'server', which is hosted on your local network.
It's a TCP server with no security measures.