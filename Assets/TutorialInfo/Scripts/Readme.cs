using System;
using UnityEngine;

/// <summary>
/// ScriptableObject for storing tutorial and documentation information.
/// Used to create interactive readme assets that display project information,
/// setup instructions, and relevant links in the Unity Inspector.
/// </summary>
public class Readme : ScriptableObject
{
    public Texture2D icon;
    public string title;
    public Section[] sections;
    public bool loadedLayout;

    /// <summary>
    /// Represents a section in the readme with heading, content, and optional web link.
    /// </summary>
    [Serializable]
    public class Section
    {
        public string heading, text, linkText, url;
    }
}
