using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class GameNetworkManager : NetworkManager {

    public const short SyncBodyMessage = 1000;

    private void Awake()
    {
        NetworkServer.RegisterHandler(SyncBodyMessage, SyncBody.ServerReadBodyUpdate);
    }

    public override void OnStartClient(NetworkClient client)
    {
        base.OnStartClient(client);
        client.RegisterHandler(SyncBodyMessage, SyncBody.ClientReadBodyUpdate);
    }

    

}

public static class Channels
{
    public const int reliableSequenced = 0;
    public const int unreliable = 1;
       
}
