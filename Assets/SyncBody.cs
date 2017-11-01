using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

[RequireComponent(typeof(Rigidbody))]
public class SyncBody : NetworkBehaviour {

    public int fixedUpdatesPerSend;

    private int fixedUpdateCounter = 0;
    private float extrapolationTime;
    private Rigidbody body;

    private State startState;
    private State targetState;
    private float lastRecievedUpdateTime;

    private Vector3 prevFramePos;
    private Vector3 instantaneousVel;
    private NetworkIdentity networkIdentity;

    StateMessage stateUpdateMessage = new StateMessage();

    [System.Serializable]
    public struct State{
        public Vector3 pos;
        public Vector3 velocity;
        public Quaternion rotation;
    }

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
        prevFramePos = body.position;
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
                SendMessage();
            }
        }
        // blend to target position and extrapolate if we are not the authority
        if (!hasAuthority)
        {
            float blendingValue = Mathf.InverseLerp(lastRecievedUpdateTime, lastRecievedUpdateTime + extrapolationTime, Time.time);
            Vector3 velBlending = startState.velocity + (targetState.velocity - startState.velocity) * blendingValue;
            Vector3 projectionFromCurrentState = startState.pos + velBlending * (Time.time - lastRecievedUpdateTime);
            Vector3 projectionFromTargetState = targetState.pos + targetState.velocity * (Time.time - lastRecievedUpdateTime);
            Vector3 blendedPosition = projectionFromCurrentState + (projectionFromTargetState - projectionFromCurrentState) * blendingValue;
            body.MovePosition(blendedPosition);
            instantaneousVel = (blendedPosition - prevFramePos) / Time.fixedDeltaTime;
            prevFramePos = blendedPosition;
        }
    }

    void SendMessage()
    {
        fixedUpdateCounter = 0;
        // if we are server, mark dirty bit so that on serialize is called
        if (isServer)
        {
            SetDirtyBit(1);
        }
        // otherwise manually send a message to notify the server where we are now
        else if(hasAuthority)
        {
            stateUpdateMessage.id = networkIdentity.netId;
            stateUpdateMessage.state.pos = body.position;
            stateUpdateMessage.state.velocity = body.velocity;
            stateUpdateMessage.state.rotation = body.rotation;
            stateUpdateMessage.sendTime = Network.time;
            NetworkManager.singleton.client.Send(NetworkMessageManager.SyncBodyMessage, stateUpdateMessage);
        }
    }

    public static void ServerReadBodyUpdate(NetworkMessage message)
    {
        StateMessage stateMessage = message.ReadMessage<StateMessage>();
        GameObject localObject = NetworkServer.FindLocalObject(stateMessage.id);
        SyncBody body = localObject.GetComponent<SyncBody>();
    }

    void ReadNetworkMessage(StateMessage message)
    {
        SetInstantaneousVel();
        targetState = message.state;
        Vector3 extrapolatedPos = targetState.pos + targetState.velocity * (float)(Network.time - message.sendTime);
        targetState.pos = extrapolatedPos;
        lastRecievedUpdateTime = Time.time;
    }

    public override bool OnSerialize(NetworkWriter writer, bool initialState)
    {
        writer.Write(body.position);
        writer.Write(body.velocity);
        writer.Write(body.rotation);
        writer.Write(Network.time);
        return true;
    }

    void SetInstantaneousVel()
    {
        // snap to our current state for the start of interpolation
        startState.pos = body.position;
        startState.velocity = instantaneousVel;
        startState.rotation = body.rotation;
    }

    public override void OnDeserialize(NetworkReader reader, bool initialState)
    {
        if (!hasAuthority)
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
