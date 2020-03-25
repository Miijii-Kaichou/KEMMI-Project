﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

using TMPro;

using PARSER = DSLParser.DialogueSystemParser;

public class DialogueSystem : MonoBehaviour
{
    public static DialogueSystem Instance;

    public enum TextSpeed
    {
        SLOWEST,
        SLOWER,
        SLOW,
        NORMAL,
        FAST,
        FASTER,
        FASTEST
    }


    [SerializeField]
    private string dsfName;

    [SerializeField]
    private TextMeshProUGUI TMP_DIALOGUETEXT;

    [SerializeField]
    private Image textBoxUI;

    private static TextSpeed text_Speed;

    private static float textSpeed;

    public static List<string> dialogue = new List<string>();

    public static bool runningDialogue { get; private set; } = false;

    public static uint lineIndex { get; private set; } = 0;

    public static uint cursorPosition { get; private set; } = 0;

    private static bool typeIn;

    const int reset = 0;
    const string BOLD = "<b>", BOLDEND = "</b>";
    const string ITALIZE = "<i>", ITALIZEEND = "</i>";
    const string UNDERLINE = "<u>", UNDERLINEEND = "</u>";
    const string SPEED = "sp=";
    const string dslFileExtention = ".dsf";
    const string STRINGNULL = "";
    const bool SUCCESSFUL = true;
    const bool FAILURE = false;

    void Awake()
    {
        Instance = this;

    }

    // Start is called before the first frame update
    void Start()
    {
        REQUEST_DIALOGUE_SET(0);
        Run();
    }

    public static void Run()
    {
        if (dialogue.Count != 0 && InBounds((int)lineIndex, dialogue) && IS_TYPE_IN() == false)
        {
            //We'll parse the very first dialogue that is ready to be displayed
            dialogue[(int)lineIndex] = PARSER.PARSER_LINE(dialogue[(int)lineIndex]);

            Instance.StartCoroutine(PrintCycle());
            Instance.StartCoroutine(ExclusionCycle());
        }
    }

    static IEnumerator ExclusionCycle()
    {
        while (true)
        {
            ExcludeAllTags(dialogue[(int)lineIndex]);
            yield return null;
        }
    }

    static IEnumerator PrintCycle()
    {

        while (true)
        {
            if (IS_TYPE_IN() == false)
            {
                ENABLE_DIALOGUE_BOX();

                GET_TMPGUI().text = STRINGNULL;

                var text = STRINGNULL;

                if (lineIndex < dialogue.Count) text = dialogue[(int)lineIndex];

                //Typewriter effect
                if (PARSER.LINE_HAS(text, PARSER.tokens[0]))
                {
                    for (cursorPosition = 0; cursorPosition < text.Length - PARSER.tokens[0].Length + 1; cursorPosition++)
                    {

                        try
                        {
                            if (lineIndex < dialogue.Count) text = dialogue[(int)lineIndex];

                            UPDATE_TEXT_SPEED(text_Speed);

                            GET_TMPGUI().text = text.Substring(0, (int)cursorPosition);
                        }
                        catch { }

                        yield return new WaitForSeconds(textSpeed);
                    }
                }

                SET_TYPE_IN_VALUE(true);
                Instance.StartCoroutine(WaitForResponse());

            }

            yield return null;
        }

    }

    static void ExcludeAllTags(string _text)
    {
        //Bold tag
        ExcludeStyleTag(BOLD, BOLDEND, ref _text);

        //Italize tag
        ExcludeStyleTag(ITALIZE, ITALIZEEND, ref _text);

        //Underline tag
        ExcludeStyleTag(UNDERLINE, UNDERLINEEND, ref _text);

        //Speed Command Tag: It will consider all of the possible values.
        for (int value = 0; value < Enum.GetValues(typeof(TextSpeed)).Length; value++)
            ExecuteSpeedFunctionTag(PARSER.delimiters[0] + SPEED + value + PARSER.delimiters[1], ref _text);
    }

    static bool ExcludeStyleTag(string _openTag, string _closeTag, ref string _line)
    {
        try
        {
            if (_line.Substring((int)cursorPosition, _openTag.Length).Contains(_openTag))
            {
                ShiftCursorPosition(_openTag.Length);
                return SUCCESSFUL;
            }

            else if (_line.Substring((int)cursorPosition, _closeTag.Length).Contains(_closeTag))
            {
                ShiftCursorPosition(_closeTag.Length);
                return SUCCESSFUL;
            }
            else
                return FAILURE;
        }
        catch { }
        return FAILURE;
    }

    static void ExecuteSpeedFunctionTag(string _tag, ref string _line)
    {
        try
        {
            if (_line.Substring((int)cursorPosition, _tag.Length).Contains(_tag))
            {
                _line = _line.Replace(_tag, "");

                dialogue[(int)lineIndex] = _line;

                int speed = Convert.ToInt32(_tag.Split('<')[1].Split('=')[1].Split('>')[0]);

                text_Speed = (TextSpeed)speed;
            }
        }
        catch { }
    }

    static bool InBounds(int index, List<string> array) => (index >= reset) && (index < array.Count);

    public static void REQUEST_DIALOGUE_SET(int _dialogueSet)
    {
        string dsPath = Application.streamingAssetsPath + @"/" + GET_DIALOGUE_SCRIPTING_FILE();

        string line = null;

        int position = 0;

        bool foundDialogueSet = false;

        if (File.Exists(dsPath))
        {
            using (StreamReader fileReader = new StreamReader(dsPath))
            {
                while (true)
                {
                    line = fileReader.ReadLine();

                    if (line == null)
                    {
                        if (foundDialogueSet)
                            return;
                        else
                        {
                            Debug.Log("Dialogue Set " + _dialogueSet.ToString("D3, CultureInfo.InvariantCulture") + " does not exist. Try adding it to the .dsf referenced.");
                            return;
                        }
                    }

                    line.Split(PARSER.delimiters);

                    if (line.Contains("<DIALOGUE_SET_" + _dialogueSet.ToString("D3", CultureInfo.InvariantCulture) + ">"))
                    {
                        foundDialogueSet = true;
                        GetDialogue(position);
                    }

                    position++;
                }
            }
        }
        Debug.LogError("File specified doesn't exist. Try creating one in StreamingAssets folder.");
    }

    static void GetDialogue(int _position)
    {
        string dsPath = Application.streamingAssetsPath + @"/" + GET_DIALOGUE_SCRIPTING_FILE();

        string line = null;

        bool atTargetLine = false;

        if (File.Exists(dsPath))
        {
            using (StreamReader fileReader = new StreamReader(dsPath))
            {
                int position = 0;

                while (true)
                {
                    line = fileReader.ReadLine();

                    if (line == "<END>" && atTargetLine)
                    {
                        runningDialogue = true;
                        return;
                    }

                    if (position > _position)
                    {
                        atTargetLine = true;
                        if (line != STRINGNULL && line[0] == '@') dialogue.Add(line);
                    }

                    position++;
                }
            }
        }
    }

    public static IEnumerator WaitForResponse()
    {
        while (IS_TYPE_IN())
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                if (lineIndex < dialogue.Count - 1)
                {
                    Progress();

                }
                else
                {
                    runningDialogue = false;
                    lineIndex = 0;
                    SET_TYPE_IN_VALUE(false);
                    DISABLE_DIALOGUE_BOX();
                    dialogue.Clear();
                    Instance.StopAllCoroutines();
                }
            }

            yield return null;
        }
    }

    public static void Progress()
    {
        if (lineIndex < dialogue.Count - 1 && IS_TYPE_IN() == true)
        {
            lineIndex += 1;

            GET_TMPGUI().text = STRINGNULL;
            SET_TYPE_IN_VALUE(false);

            //We'll parse the next line.
            dialogue[(int)lineIndex] = PARSER.PARSER_LINE(dialogue[(int)lineIndex]);
        }
    }

    public static uint ShiftCursorPosition(int _newPosition)
    {
        cursorPosition += (uint)_newPosition;
        return cursorPosition;
    }

    public static uint ShiftCursorPosition(int _newPosition, string _tag, string _removeFrom)
    {
        cursorPosition += (uint)_newPosition;
        _removeFrom = _removeFrom.Replace(_tag, "");
        return cursorPosition;
    }

    public static void UPDATE_TEXT_SPEED(TextSpeed _textSpeed)
    {
        switch (_textSpeed)
        {
            case TextSpeed.SLOWEST: textSpeed = 0.25f; return;
            case TextSpeed.SLOWER: textSpeed = 0.1f; return;
            case TextSpeed.SLOW: textSpeed = 0.05f; return;
            case TextSpeed.NORMAL: textSpeed = 0.025f; return;
            case TextSpeed.FAST: textSpeed = 0.01f; return;
            case TextSpeed.FASTER: textSpeed = 0.005f; return;
            case TextSpeed.FASTEST: textSpeed = 0.0025f; return;
            default: return;
        }
    }

    public static string GET_DIALOGUE_SCRIPTING_FILE() => Instance.dsfName + dslFileExtention;

    public static bool IS_TYPE_IN() => typeIn;

    public static void SET_TYPE_IN_VALUE(bool value) { typeIn = value; }

    public static TextMeshProUGUI GET_TMPGUI() => Instance.TMP_DIALOGUETEXT;

    public static void ENABLE_DIALOGUE_BOX() => Instance.textBoxUI.gameObject.SetActive(true);

    public static void DISABLE_DIALOGUE_BOX() => Instance.textBoxUI.gameObject.SetActive(false);

    public void OnDisable()
    {
        StopAllCoroutines();
    }
}