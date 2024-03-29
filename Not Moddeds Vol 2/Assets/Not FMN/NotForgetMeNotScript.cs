﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using KModkit;

public class NotForgetMeNotScript : MonoBehaviour
{
    public KMBombInfo Bomb;
    public KMAudio Audio;
    public KMBombModule Module;

    public KMSelectable[] buttons;
    public KMSelectable zeroButton;
    public MeshRenderer[] leds;
    public TextMesh bottomDisplay, rightDisplay, submissionScreen;

    private readonly int[] table = new int[]
    {
        1, 7, 3, 2, 6, 5, 0, 4, 1, 8,
        0, 8, 1, 5, 2, 6, 2, 3, 4, 7,
        2, 1, 6, 3, 8, 4, 5, 7, 0, 4,
        7, 3, 4, 2, 4, 1, 8, 6, 8, 0,
        8, 5, 7, 4, 5, 0, 1, 3, 2, 6,
        4, 2, 5, 6, 1, 8, 5, 0, 7, 3,
        6, 0, 8, 7, 3, 2, 4, 1, 6, 5,
        3, 4, 0, 1, 3, 7, 0, 8, 5, 2,
        4, 5, 6, 0, 7, 8, 6, 2, 3, 1,
        5, 6, 2, 8, 0, 3, 7, 5, 1, 4,
    };
    private int selectedDigit;
    private int[] solution;
    private int[] givenPuzzle;
    private int[] currentPuzzle;
    private int[] rightNums;
    private int[] bottomNums = new int[9];

    private bool inputting;
    private bool regrab;
    private string inputtedCode = "";
    static int moduleIdCounter = 1;
    int moduleId;
    private bool moduleSolved;

    void Awake()
    {
        moduleId = moduleIdCounter++;
        for (int i = 0; i < 9; i++)
        {
            int ix = i;
            buttons[ix].OnInteract += delegate () { ButtonPress(ix); return false; };
        }
        zeroButton.OnInteract += delegate () { ZeroPress(); return false; };

    }
    void Start()
    {
        GetSolution();
        GeneratePuzzle();
        GetPairs();
        SetSelected(0);
        DoLogging();
    }

    void ButtonPress(int pos)
    {
        buttons[pos].AddInteractionPunch(0.2f);
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, buttons[pos].transform);
        if (moduleSolved)
            return;
        int squarePos = Array.IndexOf(currentPuzzle, pos + 1);
        int zeroPos = Array.IndexOf(currentPuzzle, 0);
        if (!inputting)
            SetSelected(pos);
        else if (GetAdjacents(3, 3, squarePos).Contains(zeroPos) && pos != 8) //pressing 9 will always strike
        {
            currentPuzzle = Swap(currentPuzzle, squarePos, zeroPos);
            Debug.LogFormat("[Forget Me #{0}] Pressed {1}.", moduleId, pos + 1);
            inputtedCode += "12345678"[pos];
            SetInputScreen();
            regrab = false;
            leds[9].material.color = Color.black;
        }
        else
        {
            Debug.LogFormat("[Forget Me #{0}] Attempted to press {1}. Strike!", moduleId, pos + 1);
            Module.HandleStrike();
            regrab = true;
            leds[9].material.color = Color.green;
        }
    }
    void ZeroPress()
    {
        zeroButton.AddInteractionPunch(0.4f);
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, zeroButton.transform);
        if (moduleSolved)
            return;
        if (!inputting)
        {
            inputting = true;
            SetInputScreen();
        }
        else if (regrab)
            GoBack();
        else if (currentPuzzle.SequenceEqual(solution))
        {
            moduleSolved = true;
            Debug.LogFormat("[Forget Me #{0}] Pressed 0 with the grid {1}. This matches the solution, module solved!", moduleId, givenPuzzle.Select(x => x == 0 ? "-" : x.ToString()).Join());
            Module.HandlePass();
            Audio.PlaySoundAtTransform("wedidit", transform);
        }
        else
        {
            Debug.LogFormat("[Forget Me #{0}] Pressed 0 with the grid {1}. This is incorrect, strike!", moduleId, givenPuzzle.Select(x => x == 0 ? "-" : x.ToString()).Join());
            Audio.PlaySoundAtTransform("incorrect", transform);
            Module.HandleStrike();
            GoBack();
        }
    }

    void GetSolution()
    {
        bool[] visited = new bool[100];
        List<int> currentCells = new List<int>() { 10 * (Bomb.GetSerialNumber()[5] - '0') + (Bomb.GetSerialNumber()[2] - '0') };
        List<int> output = new List<int>();
        List<int> toBeCurrentCells = new List<int>();
        while (output.Count < 9)
        {
            foreach (int pos in currentCells)
            {
                if (!output.Contains(table[pos]))
                    output.Add(table[pos]);
                visited[pos] = true;
                toBeCurrentCells.AddRange(GetAdjacents(10, 10, pos));
            }
            currentCells = toBeCurrentCells.Where(x => !visited[x]).Distinct().OrderBy(x => x).ToList();
        }
        solution = output.ToArray();
    }
    void GoBack()
    {
        regrab = false;
        inputting = false;
        leds[9].material.color = Color.black;
        submissionScreen.gameObject.SetActive(false);
        bottomDisplay.gameObject.SetActive(true);
        inputtedCode = string.Empty;
        currentPuzzle = givenPuzzle.ToArray();
        SetSelected(0);
    }
    void GeneratePuzzle()
    {
        do
        {
            givenPuzzle = Enumerable.Range(0, 9).ToArray().Shuffle();
        } while (GetPermuations(givenPuzzle) % 2 != GetPermuations(solution) % 2);
        currentPuzzle = givenPuzzle.ToArray();
    }
    void GetPairs()
    {
        rightNums = Enumerable.Range(1, 99).ToArray().Shuffle().Take(9).ToArray();
        int[] sortedOrder = Enumerable.Range(0, 9).OrderBy(x => rightNums[x]).ToArray();
        for (int i = 0; i < 9; i++)
            bottomNums[sortedOrder[i]] = givenPuzzle[i];

    }
    void SetInputScreen()
    {
        leds[selectedDigit].material.color = Color.black;
        rightDisplay.text = "--";
        bottomDisplay.gameObject.SetActive(false);
        submissionScreen.gameObject.SetActive(true);
        submissionScreen.text = string.Empty;
        int numbersDisplayed = inputtedCode.Length < 24 ? inputtedCode.Length : (inputtedCode.Length % 12) + 12;
        string displayedsection = inputtedCode.TakeLast(numbersDisplayed).Join("").PadRight(24, '-');
        for (int i = 0; i < 24; i++)
        {
            submissionScreen.text += displayedsection[i];
            if (i % 3 == 2)
                submissionScreen.text += ' ';
            if (i == 11)
                submissionScreen.text += '\n';
        }
    }
    void DoLogging()
    {
        Debug.LogFormat("[Forget Me #{0}] Module generated with grid {1}.", moduleId, givenPuzzle.Select(x => x == 0 ? "-" : x.ToString()).Join());
        Debug.LogFormat("[Forget Me #{0}] The solution grid is {1}.", moduleId, solution.Select(x => x == 0 ? "-" : x.ToString()).Join());

    }
    void SetSelected(int sel)
    {
        leds[selectedDigit].material.color = Color.black;
        selectedDigit = sel;
        leds[selectedDigit].material.color = Color.green;
        rightDisplay.text = rightNums[selectedDigit].ToString().PadLeft(2, '0');
        bottomDisplay.text = bottomNums[selectedDigit].ToString();
    }

    IEnumerable<int> GetAdjacents(int width, int height, int pos)
    {
        if (pos > width - 1)
            yield return pos - width;
        if (pos % width != 0)
            yield return pos - 1;
        if (pos % width != width - 1)
            yield return pos + 1;
        if (pos < width * height - width)
            yield return pos + width;
    }
    int[] Swap(int[] array, int p1, int p2)
    {
        var arr = array.ToArray();
        int temp = arr[p1];
        arr[p1] = arr[p2];
        arr[p2] = temp;
        return arr;
    }
    int GetPermuations(int[] field)
    {
        int permutations = 0;
        for (int i = 0; i <= 8; i++)
            for (int j = 0; j < i; j++)
                if (field[i] != 0 && field[j] != 0 && (field[i] < field[j]))
                    permutations++;
        return permutations;
    }

#pragma warning disable 414
    private readonly string TwitchHelpMessage = @"Use !{0} cycle to cycle through all 9 pairs. Use !{0} submit 123456789 to press those number buttons.";
#pragma warning restore 414

    IEnumerator ProcessTwitchCommand(string command)
    {
        command = command.Trim().ToUpperInvariant();
        List<string> parameters = command.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
        if (command == "CYCLE")
        {
            if (inputting)
                yield return "sendtochaterror You cannot cycle the numbers now.";
            else
            {
                yield return null;
                for (int i = 0; i < 9; i++)
                {
                    buttons[i].OnInteract();
                    yield return "trycancel";
                    yield return new WaitForSeconds(1.5f);
                }
            }
        }
        else if (parameters.Count == 2 && (parameters[0] == "PRESS" || parameters[0] == "SUBMIT") && parameters[1].All(x => "1234567890".Contains(x)))
        {
            yield return null;
            foreach (char num in parameters[1])
            {
                if (num == '0')
                    zeroButton.OnInteract();
                else buttons[num - '1'].OnInteract();
                yield return new WaitForSeconds(0.05f);
            }
        }
    }

    struct QueueItem
    {
        public string Cells { get; private set; }
        public string Parent { get; private set; }
        public int Button { get; private set; }

        public QueueItem(string cells, string parent, int button)
        {
            Cells = cells;
            Parent = parent;
            Button = button;
        }
    }

    private IEnumerator TwitchHandleForcedSolve()
    {
        if (!inputting)
        {
            zeroButton.OnInteract();
            yield return new WaitForSeconds(0.05f);
        }
        var goal = solution.Join("");

        var visited = new Dictionary<string, QueueItem>();
        var q = new Queue<QueueItem>();
        q.Enqueue(new QueueItem(currentPuzzle.Join(""), null, 0));
        while (q.Count > 0)
        {
            var qi = q.Dequeue();
            if (visited.ContainsKey(qi.Cells))
                continue;
            visited[qi.Cells] = qi;
            if (qi.Cells == goal)
                break;

            int zeroPos = qi.Cells.IndexOf('0');
            foreach (var adj in GetAdjacents(3, 3, zeroPos))
            {
                var ch = qi.Cells.ToCharArray();
                var tmp = ch[zeroPos];
                ch[zeroPos] = ch[adj];
                ch[adj] = tmp;
                q.Enqueue(new QueueItem(new string(ch), qi.Cells, ch[zeroPos] - '0'));
            }
        }

        var r = goal;
        var path = new List<int>();
        while (true)
        {
            var nr = visited[r];
            if (nr.Parent == null)
                break;
            path.Add(nr.Button);
            r = nr.Parent;
        }

        for (int i = path.Count - 1; i >= 0; i--)
        {
            buttons[path[i] - 1].OnInteract();
            yield return new WaitForSeconds(.05f);
        }
        zeroButton.OnInteract();
    }
}
