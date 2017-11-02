using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct State
{
    public Vector3 pos;
    public Vector3 velocity;
    public Quaternion rotation;
}

public class InterpolationBuffer {

    
    private InterpolationState[] buffer;
    private int interpolationStartIndex = 0;
    private int mostRecentIndex = -1;
    private int stopIndex = -1;
    private float interpolationDelay;
    private float interpolationMultiplier = 1;
    private float averagedInterpolationMultiplier = 1;

    private Vector3 posPrevFrame;
    private Vector3 currentPosition;
    private float prevFrameTime;
    private float latencyDecreaseRate;
    private float decreaseMinLatencyFramesThreshold;
    private float decreaseMinLatencyMulAmount;
    private float firstSyncTime;
    private double stopTime;
    private int numPerfectFrames;
    private bool catchingUp;

    public struct InterpolationState
    {
        public State state;
        public float interpolationTime;
        public double networkSendTime;
        public bool valid;
    }

    public InterpolationBuffer(float delay, float latencyDecreaseRate, int decreaseMinLatencyFramesThreshold, float decreaseMinLatencyAmount)
    {
        buffer = new InterpolationState[(int)(delay / Time.fixedDeltaTime) * 4];
        interpolationDelay = delay;
        this.latencyDecreaseRate = latencyDecreaseRate;
        this.decreaseMinLatencyFramesThreshold = decreaseMinLatencyFramesThreshold;
        this.decreaseMinLatencyMulAmount = decreaseMinLatencyAmount;
    }

    public void PushState(State state, double sendTime)
    {
        // handle restarting from pause 
        if(stopIndex >= 0 )
        {
            if (sendTime > stopTime)
            {
                InterpolationState nextState;
                nextState.state = state;
                nextState.valid = true;
                nextState.networkSendTime = sendTime;
                nextState.interpolationTime = Time.time + Time.fixedDeltaTime;
                buffer[1] = nextState;
                firstSyncTime = Time.time;
                buffer[0] = buffer[interpolationStartIndex];
                buffer[0].interpolationTime = Time.time;
                buffer[0].valid = true;
                mostRecentIndex = 1;
                interpolationStartIndex = 0;
                stopIndex = -1;
                //Debug.Log("Starting");
            }
            return;
        }

        if (catchingUp)
        {
            Debug.Log("Increasing delay");
            numPerfectFrames = 0;
            interpolationMultiplier *= 1.5f;
            averagedInterpolationMultiplier = interpolationMultiplier * .5f + averagedInterpolationMultiplier * .5f;
            catchingUp = false;
        }


        InterpolationState newState;
        newState.state = state;
        newState.valid = true;
        newState.networkSendTime = sendTime;

        if (mostRecentIndex >= 0)
        {
            InterpolationState prevState = buffer[mostRecentIndex];
            if (sendTime > prevState.networkSendTime)
            {
                newState.interpolationTime = prevState.interpolationTime + (float)(newState.networkSendTime - prevState.networkSendTime);
                mostRecentIndex++;
                while (mostRecentIndex >= buffer.Length)
                {
                    mostRecentIndex -= buffer.Length;
                }
            }
            else
            {
                return;
            }
        }
        else
        {
            firstSyncTime = Time.time;
            newState.interpolationTime = Time.time;
            mostRecentIndex = 0;
        }


        buffer[mostRecentIndex] = newState;
    }

    public void SetStopPoint(double sendTime, Vector3 curPos)
    {
        Debug.Log("stop update");
        stopIndex = mostRecentIndex;
        stopTime = sendTime;
        buffer[interpolationStartIndex].state.pos = curPos;
        catchingUp = false;
        if (Application.isEditor)
        {
            //Debug.Log("stopping");
        }

    }

    public Vector3 GetInterpolatedPosition(float time)
    {
        float interpolatedTime = time - (interpolationDelay * interpolationMultiplier);
        int newStartIndex = GetStartIndex(interpolationStartIndex, interpolatedTime);
        if(newStartIndex != interpolationStartIndex && newStartIndex != stopIndex)
        {
            Vector3 deltaPos = currentPosition - posPrevFrame;
            Vector3 vel = deltaPos / (interpolatedTime - prevFrameTime);
            buffer[newStartIndex].interpolationTime = prevFrameTime;
            buffer[newStartIndex].state.pos = currentPosition;
            buffer[newStartIndex].state.velocity = vel;
        }
        while(interpolationStartIndex != newStartIndex)
        {
            buffer[interpolationStartIndex].valid = false;
            interpolationStartIndex = GetNextStateIndex(interpolationStartIndex);
        }
        if(interpolationStartIndex == stopIndex)
        {
            buffer[interpolationStartIndex].interpolationTime += Time.fixedDeltaTime;
            return buffer[interpolationStartIndex].state.pos;
        }
        InterpolationState startState = buffer[interpolationStartIndex];
        int endIndex = GetNextStateIndex(interpolationStartIndex);
        InterpolationState endState = buffer[endIndex];
        
        if (startState.valid && endState.valid && newStartIndex != endIndex && startState.interpolationTime < endState.interpolationTime && interpolatedTime > startState.interpolationTime)
        {
            catchingUp = false;
            numPerfectFrames += 1;
            if(numPerfectFrames > decreaseMinLatencyFramesThreshold)
            {
                averagedInterpolationMultiplier -= averagedInterpolationMultiplier * decreaseMinLatencyMulAmount;
                numPerfectFrames = 0;
            }
            interpolationMultiplier = Mathf.Clamp(interpolationMultiplier - interpolationMultiplier * latencyDecreaseRate, averagedInterpolationMultiplier, 5);
            float lerpAmount = (interpolatedTime - startState.interpolationTime) / (endState.interpolationTime - startState.interpolationTime);

            Vector3 result = Vector3.Lerp(startState.state.pos, endState.state.pos, lerpAmount);
            //Vector3 result = HermiteInterpolation(startState.state.pos, endState.state.pos, startState.state.velocity, endState.state.velocity, lerpAmount, 1f);
            if (Application.isEditor)
            {
                //Debug.Log(interpolationStartIndex + " " + endIndex + " " + (interpolatedTime - startState.interpolationTime) + " " + startState.interpolationTime + " " + endState.interpolationTime + " " + interpolatedTime + " " + lerpAmount);
            }
            posPrevFrame = currentPosition;
            currentPosition = result;
            prevFrameTime = interpolatedTime;
            return result;
        }
        else
        {
            if (!catchingUp && interpolatedTime > firstSyncTime && stopIndex < 0 )
            {
                catchingUp = true;
            }
        }
        posPrevFrame = currentPosition;
        currentPosition = startState.state.pos;
        prevFrameTime = interpolatedTime;
        return startState.state.pos;
    }

    public Vector3 HermiteInterpolation(Vector3 startPoint, Vector3 endPoint, Vector3 startVel, Vector3 endVel, float t, float s)
    {
        float a = 2 * t * t * t - 3 * t * t + 1;
        float d = -2*t * t * t + 3 * t * t;
        float u = t * t *t - 2 * t * t + t;
        float v = t * t * t - t * t;
        return startPoint * a + endPoint * d + startVel * u + endVel * v;

    }

    public int GetStartIndex(int currentIndex, float interpolatedTime)
    {
        int nextIndex = GetNextStateIndex(currentIndex);
        int finalIndex = GetNextStateIndex(mostRecentIndex);
        while(nextIndex != finalIndex && buffer[nextIndex].valid && buffer[nextIndex].interpolationTime < interpolatedTime)
        {
            currentIndex = nextIndex;
            nextIndex = GetNextStateIndex(currentIndex);
        }
        return currentIndex;
    }

    public int GetNextStateIndex(int index)
    {
        int nextIndex = index + 1;
        while(nextIndex >= buffer.Length)
        {
            nextIndex -= buffer.Length;
        }
        return nextIndex;
    }
}
