using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using KModkit;
using Rnd = UnityEngine.Random;

public class dandysFloors : MonoBehaviour
{
    [SerializeField] private KMBombInfo Bomb;
    [SerializeField] private KMAudio Audio;

    [SerializeField] List<Material> EnemyMaterials;
    [SerializeField] List<Material> EnemySilhouetteMaterials;

    [SerializeField] KMSelectable DandyIcon;
    [SerializeField] GameObject StageMode;
    [SerializeField] GameObject StrikeMode;
    [SerializeField] GameObject SubmissionMode;
    [SerializeField] TextMesh FloorText;
    [SerializeField] TextMesh MachinesText;
    [SerializeField] List<MeshRenderer> EnemyRenderers;
    [SerializeField] List<KMSelectable> EnemySelectables;
    
    static int ModuleIdCounter = 1;
    int ModuleId;
    private bool ModuleSolved;
    private string[] ignoredModules;

    List<string> ToonNames = new List<string>() { "Boxten", "Cosmo", "Poppy", "Looey", "Shrimpo", "Tisha", "Brightney", "Connie", "Finn", "Razzle & Dazzle", "Rodger", "Teagan", "Toodles", "Flutter", "Gigi", "Glisten", "Goob", "Scraps", "Astro", "Pebble", "Shelly", "Sprout", "Vee", "Dandy" };
    string base36 = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    string initSeed = "";
    string fullSeed = "";
    string usingSeed = "";

    float cooldown = 0;
    bool canStart;
    int lastSolves, curSolves = 0;
    bool inStages, inSubmission;

    bool done;
    int floor, machines;
    bool[] enemies = new bool[24];

    void Awake()
    {
        ModuleId = ModuleIdCounter++;
        GetComponent<KMBombModule>().OnActivate += delegate () { canStart = true; };
        DandyIcon.OnInteract += delegate () { DandyIconPress(); return false; };
        /*
        foreach (KMSelectable object in keypad) {
            object.OnInteract += delegate () { keypadPress(object); return false; };
            }
        */

        //button.OnInteract += delegate () { buttonPress(); return false; };
    }

    void DandyIconPress()
    {
        if (ModuleSolved) return;
        DandyIcon.AddInteractionPunch();
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, DandyIcon.transform);
        if (!done)
        {
            GetComponent<KMBombModule>().HandleStrike();
            Log("You pressed the flower button when you shouldn't have. Strike!");
        }
        else EnterSubmissionMode();
    }

    void EnterStageMode()
    {
        inStages = true;
        DandyIcon.gameObject.SetActive(false);
        StageMode.SetActive(true);
        GetComponent<KMSelectable>().UpdateChildrenProperly();
    }

    void EnterStrikeMode()
    {
        StageMode.SetActive(false);
        StrikeMode.SetActive(true);
        GetComponent<KMSelectable>().UpdateChildrenProperly();
    }

    void EnterSubmissionMode()
    {
        inSubmission = true;
        DandyIcon.gameObject.SetActive(false);
        StageMode.SetActive(false);
        SubmissionMode.SetActive(true);
        GetComponent<KMSelectable>().UpdateChildrenProperly();
        Log("Entering submission mode...");
    }

    void GenerateStage()
    {
        floor++;
        FloorText.text = floor.ToString();
        Log($"Generating Floor #{floor}:");

        int enemyCount = 2 + (int)floor / 10;
        int idx = 0;
        enemies = new bool[24];
        while (enemies.Count(e => e) != enemyCount)
        {
            int upper = idx < 6 ? 2 : (idx < 13 ? 3 : (idx < 18 ? 5 : (idx != 23 ? 8 : 10)));
            enemies[idx] = Rnd.Range(0, upper) == 1;
            idx = (idx + 1) % 24;
        }
        for (int i = 0; i < 24; i++)
        {
            Log((enemies[i] ? 0 : 1).ToString());
            EnemyRenderers[i].material = enemies[i] ? EnemyMaterials[i] : EnemySilhouetteMaterials[i];
        }
    }

    void Start()
    {
        if (ignoredModules == null)
            ignoredModules = GetComponent<KMBossModule>().GetIgnoredModules("Dandy's Floors", new string[]{
                "14",
                "42",
                "501",
                "A>N<D",
                "Bamboozling Time Keeper",
                "Black Arrows",
                "Brainf---",
                "The Board Walk",
                "Busy Beaver",
                "Dandy's Floors",
                "Don't Touch Anything",
                "Floor Lights",
                "Forget Any Color",
                "Forget Enigma",
                "Forget Ligma",
                "Forget Everything",
                "Forget Infinity",
                "Forget It Not",
                "Forget Maze Not",
                "Forget Me Later",
                "Forget Me Not",
                "Forget Perspective",
                "Forget The Colors",
                "Forget Them All",
                "Forget This",
                "Forget Us Not",
                "Iconic",
                "Keypad Directionality",
                "Kugelblitz",
                "Multitask",
                "OmegaDestroyer",
                "OmegaForest",
                "Organization",
                "Password Destroyer",
                "Purgatory",
                "Reporting Anomalies",
                "RPS Judging",
                "Security Council",
                "Shoddy Chess",
                "Simon Forgets",
                "Simon's Stages",
                "Souvenir",
                "Tallordered Keys",
                "The Time Keeper",
                "Timing is Everything",
                "The Troll",
                "Turn The Key",
                "The Twin",
                "Übermodule",
                "Ultimate Custom Night",
                "The Very Annoying Button",
                "Whiteout"
            });

        CalculateSeed();
        StageMode.gameObject.SetActive(false);
        StrikeMode.gameObject.SetActive(false);
        SubmissionMode.gameObject.SetActive(false);
    }

    void Update()
    {
        if (canStart && !inSubmission)
        {
            int curSolves = Bomb.GetSolvedModuleNames().Where(a => !ignoredModules.Contains(a)).ToList().Count;
            if (curSolves == Bomb.GetModuleNames().Where(a => !ignoredModules.Contains(a)).ToList().Count) EnterSubmissionMode();
            if (cooldown > 0)
                cooldown -= Time.deltaTime;
            else
            {
                if (curSolves > lastSolves)
                {
                    if (!inStages)
                    {
                        EnterStageMode();
                        inStages = true;
                    }
                    GenerateStage();
                    //cooldown = 10f;
                }
                lastSolves = curSolves;
            }
        }
    }

    void CalculateSeed()
    {
        Log("Generating the seed:");

        for (int i = 0; i < 6; i++)
        {
            string shiftSN = Bomb.GetSerialNumber().Substring(i, 6 - i) + Bomb.GetSerialNumber().Substring(0, i);
            string convSN = base36toBinary(shiftSN);
            Log($"{shiftSN} into binary: {convSN} ({convSN.Length} bits{(convSN.Length > 31 ? ", which is too long" : "")})");
            if (convSN.Length <= 31)
            {
                initSeed = convSN;
                fullSeed = initSeed;
                break;
            }
        }
        if (initSeed == "")
        {
            Log("Could not generate the run's seed - submit 0 ichor!");
            done = true;
        }
        else
        {
            Log($"Initial seed: {fullSeed}.");

            Log($"Generating the rest of the seed:");
            string curSN = Bomb.GetSerialNumber();
            for (int i = 1; i < 36; i++)
            {
                curSN = base36Caesar(curSN);
                string newBits = base36toBinary(curSN);
                if (newBits.Length < initSeed.Length) newBits = new string('0', initSeed.Length - newBits.Length) + newBits;
                string truncatedBits = newBits.Substring(newBits.Length - initSeed.Length, initSeed.Length);
                string xor = Convert.ToString(Convert.ToInt32(initSeed, 2) ^ Convert.ToInt32(truncatedBits, 2), 2);
                if (xor.Length < initSeed.Length) xor = new string('0', initSeed.Length - xor.Length) + xor;
                Log($"Iteration {i}: {curSN} -> {newBits} -> {truncatedBits} XOR {initSeed} -> {xor}");
                fullSeed += xor;
            }
            Log($"Full seed: {fullSeed}.");
            usingSeed = fullSeed;
        }
    }

    //int Damage(int enemies, int stageNumber, int lastBlackout, int batteries, int dandyPoints, int playerNumber, int machines, int ichor, int proteinBarsEaten)
    //{
    //    //000000 0000000 00000 00000 0
    //    //common uncommn rare  main  lethal
    //    int res = 0;
    //    if (enemies & 0x80)
    //    { //Goob check
    //        if (!isPrime(stageNumber)) res++
    //        else if (enemies & 1) res += 3 else return 0;
    //    }
    //    if (enemies & 0x20) //Astro
    //    {
    //        machines = 1.25 * machines + batteries;
    //        if ((machines > 7) && (machines % 4 == 0)) res++;
    //    }
    //    if ((enemies & 0x800000) && isPrime(stageNumber)) res++; //Boxten
    //    if ((enemies & 0x400000) && (enemies & 0x7)) res++; //Cosmo
    //    if ((enemies & 0x200000) && (isFibbonacci(stageNumber)) && (stageNumber > 7)) res++; //Poppy
    //    if ((enemies & 0x100000) && inInventory(11)) { res++; useID(11); } //Looey
    //    if ((enemies & 0x80000) && (machines > 8) && (playerNumber != 9)) res++; //Shrimpo
    //    if ((enemies & 0x40000) && (enemiesCount(enemies) == 6)) res++; //Tisha
    //    if ((enemies & 0x20000) && (stageNumber - lastBlackout == 1)) res++; //Brightney
    //    if ((enemies & 0x10000) && (enemies & 0x10020)) res++; //Connie
    //    if ((enemies & 0x8000) && ((machines > 8) || (playerNumber == 5))) res++; //Finn
    //    //if ((enemies & 0x4000) res+=0; //Razzle & Dazzle
    //    if ((enemies & 0x2000) && inInventory(0) && (ichor < 160) && (maxID() > 11) && !inInventory(15)) res++; //Rodger
    //    if ((enemies & 0x1000) && (ichor > 600)) { res++; ichor -= 100; } //Teagan
    //    if ((enemies & 0x800) && (enemies & 0x2000)) res++; //Toodles
    //    if (enemies & 0x400) //Flutter
    //    {
    //        useAllID(3);
    //        useAllID(12);
    //        if ((playerNumber == 8) || (playerNumber == 5) || ((playerNumber == 10) && (stageNumber % 2 == 0))) res++;
    //    }
    //
    //    if (enemies & 0x100) //Glisten
    //    {
    //        if (machines == 4) res++;
    //        if ((playerNumber == 5) || (playerNumber == 16)) res++;
    //    }
    //    if (enemies & 0x40) //Scraps
    //    {
    //        if (stageNumber % 3) res++;
    //        if (enemies & 0x80) res++;
    //    }
    //    if ((enemies & 0x10) && ((stageNumber % 11) ^ (enemies & 1))) res++; //Pebble
    //    if ((enemies & 0x8) && (playerNumber != 23) && (!(enemies & 0x2)) && !inInventory(7) && !inInventory(10)) res++; //Shelly
    //    if ((enemies & 0x4) && (playerNumber != 2) && (proteinBarsEaten % 2 == 0)) res++; //Sprout
    //    if (enemies & 0x2) res++; //Vee
    //    if ((enemies & 0x1) && (!(enemies & 0x3e)) && (dandyPoints % 5 == 0)) return 999; //Dandy
    //    return res;
    //}

    string base36toBinary(string n)
    {
        int dec = 0;
        for (int i = 0; i < n.Length; i++)
        {
            dec += base36.IndexOf(n[i]) * (int)Math.Pow(36, n.Length - i - 1);
        }
        return Convert.ToString(dec, 2);
    }

    string base36Caesar(string n)
    {
        string caesar = "";
        foreach (char c in n)
        {
            caesar += base36[(base36.IndexOf(c) + 1) % 36];
        }
        return caesar;
    }

    string nextBits(int n)
    {
        if (usingSeed.Length < n) usingSeed += fullSeed;
        string next = usingSeed.Substring(0, n);
        usingSeed = usingSeed.Substring(n);
        return next;
    }

    void Log(string arg)
    {
        Debug.Log($"[Dandy's Floors #{ModuleId}] {arg}");
    }

#pragma warning disable 414
    private readonly string TwitchHelpMessage = @"Use !{0} to do something.";
#pragma warning restore 414

    IEnumerator ProcessTwitchCommand(string Command)
    {
        yield return null;
    }

    IEnumerator TwitchHandleForcedSolve()
    {
        yield return null;
    }
}
