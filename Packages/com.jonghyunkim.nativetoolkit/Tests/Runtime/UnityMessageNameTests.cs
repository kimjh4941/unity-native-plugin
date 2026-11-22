#nullable enable

using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using JonghyunKim.NativeToolkit.Runtime.iOS.Common;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// No MonoBehaviour of the package declares a Unity message name with parameters. Unity takes such a
    /// method for the message and logs "Script error (…): Start() can not take parameters." whenever
    /// the domain loads, in the Editor and in a player, outside any test's LogAssert (implementation
    /// review v1 of the iOS Dialog move, R-1: a helper was named Start).
    /// </summary>
    public sealed class UnityMessageNameTests
    {
        private static readonly string[] ParameterlessMessages =
        {
            "Awake", "Start", "Update", "LateUpdate", "FixedUpdate", "OnEnable", "OnDisable", "OnDestroy",
            "OnApplicationQuit", "Reset", "OnValidate", "OnGUI",
        };

        [Test]
        public void NoMonoBehaviourTakesParametersInAUnityMessage()
        {
            Assembly runtime = typeof(IosNativeToolkitCApi).Assembly;
            string[] offenders = runtime.GetTypes()
                .Where(t => typeof(MonoBehaviour).IsAssignableFrom(t))
                .SelectMany(t => t.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    .Where(m => ParameterlessMessages.Contains(m.Name) && m.GetParameters().Length > 0)
                    .Select(m => $"{t.FullName}.{m.Name}"))
                .ToArray();
            CollectionAssert.IsEmpty(offenders);
        }
    }
}
