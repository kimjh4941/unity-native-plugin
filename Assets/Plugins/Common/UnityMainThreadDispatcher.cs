using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class UnityMainThreadDispatcher : MonoBehaviour
{
    private static UnityMainThreadDispatcher _instance;
    private readonly Queue<Action> _executionQueue = new Queue<Action>();

    public static UnityMainThreadDispatcher Instance
    {
        get
        {
            if (_instance == null)
            {
                var gameObject = new GameObject("UnityMainThreadDispatcher");
                _instance = gameObject.AddComponent<UnityMainThreadDispatcher>();
                DontDestroyOnLoad(gameObject);
            }
            return _instance;
        }
    }

    public void Enqueue(Action action)
    {
        lock (_executionQueue)
        {
            _executionQueue.Enqueue(action);
        }
    }

    private void Update()
    {
        lock (_executionQueue)
        {
            while (_executionQueue.Count > 0)
            {
                _executionQueue.Dequeue().Invoke();
            }
        }
    }
}
