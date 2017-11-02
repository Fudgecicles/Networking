using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;


[RequireComponent(typeof(Rigidbody))]
public class SyncBody : NetworkBehaviour {

    public int fixedUpdatesPerSend;
    public int numInterpolationDelayFrames;

    public float latencyDecreaseRate;
    public int decreaseMinLatencyFrames;
    public float decreaseMinLatencyAmount;
    public float minDeltaPositionSendDistance;
    public float stopSendIdleTimer = .01f;

    private float interpolationDelay;
    private int fixedUpdateCounter = 0;
    private float extrapolationTime;
    private Rigidbody body;

    private State startState;
    private State targetState;
    private float lastRecievedUpdateTime;
    private double lastRecievedNetworkTime;

    private NetworkIdentity networkIdentity;

    private Vector3 prevSentPosition;
    private float minSendDistanceSquared;
    private float idleTimer;

    private bool doInterpolation = true;

    StateMessage stateUpdateMessage = new StateMessage();
    InterpolationBuffer buffer;

    public class StateMessage : MessageBase
    {
        public NetworkInstanceId id;
        public double sendTime;
        public State state;
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        networkIdentity = GetComponent<NetworkIdentity>();
        float numFixedUpdateInMinute = 1.0f / Time.fixedDeltaTime;
        extrapolationTime = fixedUpdatesPerSend * Time.fixedDeltaTime;
        interpolationDelay = extrapolationTime * numInterpolationDelayFrames;
        buffer = new InterpolationBuffer(interpolationDelay, latencyDecreaseRate, decreaseMinLatencyFrames, decreaseMinLatencyAmount);
        minSendDistanceSquared = minDeltaPositionSendDistance * minDeltaPositionSendDistance;
    }

    private void Start()
    {
        // do this in start because authority is not assigned on awake
        if (!hasAuthority)
        {
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
        }
    }

    private void FixedUpdate()
    {
        // we only send messages if we are the server or authority
        if (isServer || hasAuthority)
        {
            fixedUpdateCounter += 1;
            if (fixedUpdateCounter >= fixedUpdatesPerSend)
            {
                if ((prevSentPosition - body.position).sqrMagnitude > minSendDistanceSquared)
                {
                    if (Application.isEditor) {
                        //Debug.Log("Sending");
                    }
                    idleTimer = 0;
                    doInterpolation = true;
                    SendMessage();
                }
                else if(doInterpolation)
                {
                    idleTimer += Time.fixedDeltaTime;
                    if (idleTimer > stopSendIdleTimer)
                    {
                        if (Application.isEditor)
                        {
                            //Debug.Log("stopping");
                        }
                        doInterpolation = false;
                        if (isServer)
                        {
                            RpcDisableInterpolation(Network.time);
                        }
                        else
                        {
                            CmdDisableInterpolation(Network.time);
                        }

                    }
                }
            }
        }
        // blend to target position and extrapolate if we are not the authority
        if (!hasAuthority)
        {
            body.MovePosition(buffer.GetInterpolatedPosition(Time.time));   
        }
    }

    [Command]
    void CmdDisableInterpolation(double sendTime)
    {
        buffer.SetStopPoint(sendTime, body.position);
        RpcDisableInterpolation(sendTime);
    }

    [ClientRpc]
    void RpcDisableInterpolation(double sendTime)
    {
        if (!isServer && !hasAuthority)
        {
            buffer.SetStopPoint(sendTime, body.position);
        }
    }

    void SendMessage()
    {
        fixedUpdateCounter = 0;
        
        stateUpdateMessage.id = networkIdentity.netId;
        stateUpdateMessage.state.pos = body.position;
        stateUpdateMessage.state.velocity = body.velocity;
        stateUpdateMessage.state.rotation = body.rotation;
        stateUpdateMessage.sendTime = Network.time;
        prevSentPosition = body.position;
        // server should update all clients on where the character's position is
        if (isServer)
        {
            for(int k=0; k < NetworkServer.connections.Count; k++)
            {
                if(NetworkServer.connections[k] != null)
                {
                    NetworkServer.connections[k].SendByChannel(GameNetworkManager.SyncBodyMessage, stateUpdateMessage, Channels.unreliable);
                }
            }
        }
        // otherwise send a message to notify the server where we are now
        else if(hasAuthority)
        {
            NetworkManager.singleton.client.connection.SendByChannel(GameNetworkManager.SyncBodyMessage, stateUpdateMessage, Channels.unreliable);
        }
    }

    public static void ServerReadBodyUpdate(NetworkMessage message)
    {
        StateMessage stateMessage = message.ReadMessage<StateMessage>();
        GameObject localObject = NetworkServer.FindLocalObject(stateMessage.id);
        if (localObject != null)
        {
            SyncBody body = localObject.GetComponent<SyncBody>();
            body.ReadNetworkMessage(stateMessage);
        }
    }

    public static void ClientReadBodyUpdate(NetworkMessage message)
    {
        StateMessage stateMessage = message.ReadMessage<StateMessage>();
        GameObject localObject = ClientScene.FindLocalObject(stateMessage.id);
        if (localObject != null)
        {
            SyncBody body = localObject.GetComponent<SyncBody>();
            // if we are hosting on a client then don't read our own position updates
            if (!body.isServer)
            {
                
                body.ReadNetworkMessage(stateMessage);
            }
        }
    }

    void ReadNetworkMessage(StateMessage message)
    {
        if (!hasAuthority)
        {
            targetState = message.state;
            lastRecievedUpdateTime = Time.time;
            lastRecievedNetworkTime = message.sendTime;
            buffer.PushState(message.state, message.sendTime);
            doInterpolation = true;
        }
    }

    public static IEnumerator DrawRecievedMessage(Vector3 pos, Color startCol, float duration = 3)
    {
        float timer = 0;
        while(timer < duration)
        {
            timer += Time.deltaTime;
            Debug.DrawLine(pos + Vector3.forward * .5f, pos - Vector3.forward * .5f, startCol);
            Debug.DrawLine(pos + Vector3.left * .5f, pos - Vector3.left * .5f, startCol);

            yield return null;
        }
    }

    public override bool OnSerialize(NetworkWriter writer, bool initialState)
    {
        writer.Write(body.position);
        writer.Write(body.velocity);
        writer.Write(body.rotation);
        writer.Write(Network.time);
        prevSentPosition = body.position;
        return true;
    }

    public override void OnDeserialize(NetworkReader reader, bool initialState)
    {
        if (!hasAuthority && !isServer)
        {
            stateUpdateMessage.state.pos = reader.ReadVector3();
            stateUpdateMessage.state.velocity = reader.ReadVector3();
            stateUpdateMessage.state.rotation = reader.ReadQuaternion();
            stateUpdateMessage.sendTime = reader.ReadDouble();

            ReadNetworkMessage(stateUpdateMessage);

            // snap to position if this is the intial state
            if (initialState)
            {
                startState = targetState;
                body.MovePosition(targetState.pos);
                body.velocity = targetState.velocity;
                body.rotation = targetState.rotation;
            }
        }
    }

}
