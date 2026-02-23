#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Jonghyunkim.NativeToolkit.Editor.Build.Tools
{
    internal static class BuildEnvUtil
    {
        // 既定の探索順: <指定パス> → プロジェクト直下/.env.nativetoolkit → プロジェクト直下/.env
        public static (string[] extraDefines, bool enableBuildSteps) ReadEnv(string? filePath = null)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            string path = ResolvePath(projectRoot, filePath);
            var dict = LoadKv(path);

            // NTK_ENABLE_BUILD_STEPS=true/false（既定: true）
            bool enable = true;
            if (dict.TryGetValue("NTK_ENABLE_BUILD_STEPS", out var enableStr))
                enable = IsTrue(enableStr);

            // NTK_EXTRA_DEFINES=FOO,BAR（カンマ区切り）
            string[] defines = Array.Empty<string>();
            if (dict.TryGetValue("NTK_EXTRA_DEFINES", out var csv) && !string.IsNullOrWhiteSpace(csv))
                defines = SplitCsv(csv);

            // 環境変数でも上書き可能（CI 等）
            var envEnable = Environment.GetEnvironmentVariable("NTK_ENABLE_BUILD_STEPS");
            if (!string.IsNullOrEmpty(envEnable)) enable = IsTrue(envEnable);
            var envDefines = Environment.GetEnvironmentVariable("NTK_EXTRA_DEFINES");
            if (!string.IsNullOrWhiteSpace(envDefines)) defines = SplitCsv(envDefines);

            return (defines, enable);
        }

        private static string ResolvePath(string projectRoot, string? filePath)
        {
            if (!string.IsNullOrEmpty(filePath))
            {
                var given = Path.IsPathRooted(filePath) ? filePath : Path.Combine(projectRoot, filePath);
                if (File.Exists(given)) return given!;
            }
            var candidate1 = Path.Combine(projectRoot, ".env.nativetoolkit");
            if (File.Exists(candidate1)) return candidate1;
            var candidate2 = Path.Combine(projectRoot, ".env");
            if (File.Exists(candidate2)) return candidate2;
            return string.Empty; // 見つからなければ空→既定値で進む
        }

        private static Dictionary<string, string> LoadKv(string path)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return dict;

            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                int idx = line.IndexOf('=');
                if (idx <= 0) continue;
                string key = line.Substring(0, idx).Trim();
                string val = line.Substring(idx + 1).Trim().Trim('\'', '"');
                dict[key] = val;
            }
            return dict;
        }

        private static bool IsTrue(string s)
        {
            var v = s.Trim().ToLowerInvariant();
            return v is "1" or "true" or "yes" or "on";
        }

        private static string[] SplitCsv(string s)
        {
            var parts = s.Split(',');
            var list = new List<string>(parts.Length);
            foreach (var p in parts)
            {
                var t = p.Trim();
                if (!string.IsNullOrEmpty(t)) list.Add(t);
            }
            return list.ToArray();
        }
    }
}