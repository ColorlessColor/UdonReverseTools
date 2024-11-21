#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VRC.Udon.Common;
using VRC.Udon.Common.Interfaces;
using VRC.Udon.Serialization.OdinSerializer;
using Object = UnityEngine.Object;
using UnityEditor;
using System.Text.RegularExpressions;
using System.IO;

namespace NotCat.UdonTools
{
    public class SerializedPublicVariablesBytesStringDecoder : EditorWindow
    {
        public string serializedPublicVariablesBytesString;

        public IUdonVariableTable publicVariables;

        private Vector2 scrolling1;
        private Vector2 scrolling2;

        [MenuItem("Udon Tools/Udon 参数解码")]
        public static void ShowWindow()
        {
            GetWindow<SerializedPublicVariablesBytesStringDecoder>("Udon 参数解码");
        }

        [MenuItem("Assets/Udon Tools/Udon 批量参数解码")]
        public static void BatchDecode2File()
        {
            if (Selection.assetGUIDs.Count() == 0)
            {
                Debug.LogError("请选择 Udon 程序资产");
                return;
            }
            BatchDecodeVariable2File(Selection.assetGUIDs);
        }

        static public void BatchDecodeVariable2File(string[] guids)
        {
            Regex regex = new Regex(@"serializedPublicVariablesBytesString:\s*([^ ]+)");

            foreach (var guid in guids)
            {
                var filePath = AssetDatabase.GUIDToAssetPath(guid);
                Debug.Log($"[<color=#0c824c>Udon Variable Decoder</color>] File: {filePath}");

                string text = File.ReadAllText(filePath);

                MatchCollection matches = regex.Matches(text);

                var outputPath = Path.Combine("Temp", "UdonTools", "UdonVariableDecoder", $"udon_variable_guid_{guid}.log");
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

                if (matches.Length > 0)
                {
                    using (StreamWriter outputFile = File.CreateText(outputPath))
                    {
                        foreach (Match match in matches)
                        {
                            string serializedPublicVariablesBytesString = null;
                            if (match.Success)
                            {
                                serializedPublicVariablesBytesString = match.Groups[1].Value;
                            }
                            else
                            {
                                continue;
                            }

                            if (serializedPublicVariablesBytesString != null && serializedPublicVariablesBytesString != "")
                            {
                                Debug.Log($"[<color=#0c824c>Udon Variable Decoder</color>] {filePath} Matched.");
                                IUdonVariableTable publicVariables = DecodeString(serializedPublicVariablesBytesString);
                                if (publicVariables != null)
                                {
                                    foreach (string publicVariableSymbol in publicVariables.VariableSymbols.ToArray())
                                    {
                                        publicVariables.TryGetVariableValue(publicVariableSymbol, out object value);
                                        outputFile.WriteLine($"Symbol: {publicVariableSymbol}");

                                        publicVariables.TryGetVariableType(publicVariableSymbol, out Type declaredType);
                                        if (declaredType != null)
                                        {
                                            outputFile.WriteLine($"Type: {declaredType}");
                                        }
                                        else
                                        {
                                            outputFile.WriteLine($"Type: null");
                                        }

                                        if (value != null)
                                        {
                                            outputFile.WriteLine($"RealType: {value.GetType()}");
                                            outputFile.WriteLine($"Value: {value}");
                                        }
                                        else
                                        {
                                            outputFile.WriteLine($"RealType: null");
                                            outputFile.WriteLine($"Value: null");
                                        }

                                        outputFile.WriteLine("");
                                    }
                                }
                            }
                            else
                            {
                                continue;
                            }
                        }

                    }
                    UnityEditorInternal.InternalEditorUtility.OpenFileAtLineExternal(outputPath, -1);
                    Debug.Log($"[<color=#0c824c>Udon Variable Decoder</color>] {filePath} decoded, write to {outputPath}");
                }
            }

            // if (LastoutPutFilePathToOpen != null)
            // {
            //     UnityEditorInternal.InternalEditorUtility.OpenFileAtLineExternal(LastoutPutFilePathToOpen, -1);
            // }
        }

        // [MenuItem("CONTEXT/Component/Udoon 参数解码")]
        // public static void CheckUdonDecodeOnObject(MenuCommand command)
        // {
        //     var obj = command.context;
        //     Debug.Log(obj);
        //     if (obj != null && obj is Component)
        //     {
        //         SerializedObject serializedObject = new SerializedObject(obj);
        //         var compressedProgramBytes = serializedObject.FindProperty("serializedPublicVariablesBytesString");
        //         serializedPublicVariablesBytesString = compressedProgramBytes.stringValue;
        //         ShowWindow();
        //     }
        // }

        public void OnGUI()
        {
            GUILayout.Space(20);
            GUILayout.Label("输入 serializedPublicVariablesBytesString", EditorStyles.boldLabel);
            GUILayout.Space(20);

            scrolling1 = GUILayout.BeginScrollView(scrolling1, GUILayout.Height(100));
            serializedPublicVariablesBytesString = GUILayout.TextArea(serializedPublicVariablesBytesString);
            GUILayout.EndScrollView();
            GUILayout.Space(20);

            try
            {
                if (GUILayout.Button("解码"))
                {
                    publicVariables = DecodeString(serializedPublicVariablesBytesString);
                }
            }
            catch
            {
                publicVariables = null;
            }

            if (publicVariables != null && publicVariables.VariableSymbols.Any())
            {
                GUILayout.Space(20);
                scrolling2 = GUILayout.BeginScrollView(scrolling2);

                foreach (string publicVariableSymbol in publicVariables.VariableSymbols.ToArray())
                {
                    publicVariables.TryGetVariableValue(publicVariableSymbol, out object value);
                    if (value != null)
                    {
                        GUILayout.Label("Variable Symbol Name:");
                        GUILayout.TextField(publicVariableSymbol);

                        publicVariables.TryGetVariableType(publicVariableSymbol, out Type declaredType);
                        GUILayout.Label("Variable Type:");
                        GUILayout.TextField(declaredType.ToString());

                        GUILayout.Label("Real Type:");
                        GUILayout.TextField(value.GetType().ToString());

                        GUILayout.Label("Value:");
                        DisplayKnownDatatype(value);
                    }
                    else
                    {
                        GUILayout.Label("Symbol Name:");
                        GUILayout.TextField(publicVariableSymbol);

                        publicVariables.TryGetVariableType(publicVariableSymbol, out Type declaredType);
                        GUILayout.Label("Variable Type:");
                        GUILayout.TextField(declaredType.ToString());

                        GUILayout.Label("Value: null");
                    }
                    GUILayout.Space(20);
                }
                GUILayout.EndScrollView();
            }
        }

        static public IUdonVariableTable DecodeString(string serializedPublicVariablesBytesString)
        {
            try
            {
                byte[] serializedPublicVariablesBytes =
                Convert.FromBase64String(serializedPublicVariablesBytesString ?? "");

                return VRC.Udon.Serialization.OdinSerializer.SerializationUtility.DeserializeValue<IUdonVariableTable>(
                    serializedPublicVariablesBytes,
                    DataFormat.Binary,
                    new List<Object>()
                );
            }
            catch (Exception e)
            {
                Debug.LogWarning(e);
                return null;
            }
        }

        public void DisplayKnownDatatype(object value)
        {
            if (value is string[])
            {
                string[] stringArray = (string[])value;
                foreach (var s in stringArray)
                {
                    GUILayout.TextField(s);
                }
            }
            else if (value is IEnumerable<object>)
            {
                IEnumerable<object> objs = (IEnumerable<object>)value;
                foreach (var o in objs)
                {
                    if (o != null)
                    {
                        GUILayout.TextField(o.ToString());
                    }
                }
            }
            else
            {
                GUILayout.TextField(value.ToString());
            }
        }
    }

}

#endif