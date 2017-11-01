using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class NetworkMessageManager : NetworkBehaviour {

    public const short SyncBodyMessage = 1000;

    private void Awake()
    {
        NetworkServer.RegisterHandler(SyncBodyMessage, SyncBody.ServerReadBodyUpdate);
    }
}
