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
    [SerializeField] KMSelectable StageSubmitButton;

    static int ModuleIdCounter = 1;
    int ModuleId;
    private bool ModuleSolved;
    private string[] ignoredModules;

    List<string> ToonNames = new List<string>() { "Boxten", "Cosmo", "Poppy", "Looey", "Shrimpo", "Tisha", "Brightney", "Connie", "Finn", "Razzle & Dazzle", "Rodger", "Teagan", "Toodles", "Flutter", "Gigi", "Glisten", "Goob", "Scraps", "Astro", "Pebble", "Shelly", "Sprout", "Vee", "Dandy" };
    List<string> ItemNames = new List<string>() { "Gumballs", "Chocolate", "Pop", "Speed Candy", "Protein Bar", "Stealth Candy", "Skill Check Candy", "Jumper Cable", "Bandage", "Enigma Candy", "Air Horn", "Bottle o' Pop", "Health Kit", "Box o' Chocolates", "Eject Button", "Smoke Bomb" };
    List<string> EnemyRarityNames = new List<string>() { "Common", "Uncommon", "Rare", "Main", "Lethal" };
    List<string> ItemRarityNames = new List<string>() { "Common", "Uncommon", "Rare", "Very Rare", "Ultra Rare" };
    string base36 = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    string initSeed = "";
    string fullSeed = "";
    string usingSeed = "";

    float cooldown = 0;
    bool canStart;
    int lastSolves, curSolves = 0;
    bool inStages, inSubmission;

    bool done;
    int characterNum;
    int hp;
    int ichor = 0;
    int[] inventory = new int[3] { -1, -1, -1 };
    List<int> usedRarities = new List<int>();
    int proteinBarsUsed;

    int floor, machines;
    bool[] enemies = new bool[24];
    int itemCount;
    List<int> items;
    int prevBlackout = -1;

    void Awake()
    {
        ModuleId = ModuleIdCounter++;
        GetComponent<KMBombModule>().OnActivate += delegate () { canStart = true; };
        DandyIcon.OnInteract += delegate () { DandyIconPress(); return false; };
        foreach (KMSelectable enemy in EnemySelectables) {
            enemy.gameObject.transform.Find("HL").gameObject.SetActive(false);
        }
        StageSubmitButton.OnInteract += delegate () { SubmitPress(); return false; };
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

    void SubmitPress()
    {
        StageSubmitButton.AddInteractionPunch();
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, StageSubmitButton.transform);
        if (!done)
        {
            GetComponent<KMBombModule>().HandleStrike();
            Log("You pressed the submit button when you shouldn't have. Strike!");
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
        List<string> floorEnemyNames = new List<string>();
        for (int i = 0; i < 24; i++)
        {
            EnemyRenderers[i].material = enemies[i] ? EnemyMaterials[i] : EnemySilhouetteMaterials[i];
            if (enemies[i]) floorEnemyNames.Add(ToonNames[i]);
        }
        Log($"Active enemies on this floor: {floorEnemyNames.Join(", ")}.");

        machines = Rnd.Range(4, 11);
        MachinesText.text = machines.ToString();
        if (enemies[18])
        {
            machines = (int)(machines * 1.25 + Bomb.GetBatteryCount());
            Log($"The displayed number of machines is {MachinesText.text}. Because Astro is active, the real number of machines is {MachinesText.text} * 1.25 + {Bomb.GetBatteryCount()} = {machines}.");
        }
        else Log($"There are {machines} machines on this floor.");

        string itemsBits = nextBits(2);
        int itemCount = Convert.ToInt32(itemsBits, 2) + 1;
        Log($"Requesting 2 bits: {itemsBits}. There {(itemCount > 1 ? "are" : "is")} {itemCount} item{AddS(itemCount)} on this floor:");
        items = new List<int>();
        for (int i = 0; i < itemCount; i++)
        {
            string rarityBits = nextItemRarity();
            int rarity = 0;
            switch (rarityBits.Count(b => b == '1'))
            {
                case 0:
                case 1:
                    rarity = 0;
                    break;
                case 2:
                    rarity = 1;
                    break;
                case 3:
                    rarity = 2;
                    break;
                case 4:
                    rarity = 3;
                    break;
                case 5:
                    rarity = 4;
                    break;
            }
            string itemBits = rarity == 4 ? nextBits(1) : nextBits(2);
            int item = Convert.ToInt32(itemBits, 2);
            switch (rarity)
            {
                case 0:
                    item %= 3;
                    break;
                case 1:
                    item %= 4;
                    item += 3;
                    break;
                case 2:
                    item %= 4;
                    item += 7;
                    break;
                case 3:
                    item %= 3;
                    item += 11;
                    break;
                case 4:
                    item += 14;
                    break;
            }
            items.Add(item);
            Log($"Requesting {rarityBits.Length} bit{AddS(rarityBits)}: {rarityBits}. Item rarity - {ItemRarityNames[rarity]}. Requesting {itemBits.Length} more bit{AddS(itemBits)}: {itemBits}. Item #1 is {ItemNames[item]}.");
        }
    }

    void Start()
    {
        StageMode.gameObject.SetActive(false);
        StrikeMode.gameObject.SetActive(false);
        SubmissionMode.gameObject.SetActive(false);
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
        string charBits = nextBits(23);
        characterNum = Convert.ToInt32(charBits, 2) % 23;
        hp = characterNum >= 18 ? 2 : 3;
        Log($"Requesting 23 bits: {charBits}. You are playing as Toon number {Convert.ToInt32(charBits, 2)} % 23 + 1 - {ToonNames[characterNum]}. You start with {hp} HP.");
    }

    void Update()
    {
        if (canStart && !inSubmission)
        {
            int curSolves = Bomb.GetSolvedModuleNames().Where(a => !ignoredModules.Contains(a)).ToList().Count;
            if (curSolves == Bomb.GetModuleNames().Where(a => !ignoredModules.Contains(a)).ToList().Count) EnterSubmissionMode();
            else
            {
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
    }

    void CalculateSeed()
    {
        Log("Generating the seed:");

        for (int i = 0; i < 6; i++)
        {
            string shiftSN = Bomb.GetSerialNumber().Substring(i, 6 - i) + Bomb.GetSerialNumber().Substring(0, i);
            string convSN = base36toBinary(shiftSN);
            Log($"{shiftSN} into binary: {convSN} ({convSN.Length} bit{AddS(convSN)}{(convSN.Length > 31 ? ", which is too long" : "")})");
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

    int Damage()
    {
        int res = 0;
        if (enemies[16])
        { //Goob check
            if (!isPrime(floor)) res++;
            else if (enemies[23]) res += 3;
            else return 0;
        }
        if (enemies[0] && isPrime(floor)) res++; //Boxten
        if (enemies[1] && (enemies[21] || enemies[23])) res++; //Cosmo
        if (enemies[2] && (isFibonacci(floor)) && (floor > 7)) res++; //Poppy
        if (enemies[3] && inventory.Contains(10)) res++;  //Looey (USE ONE 10)
        if (enemies[4] && machines > 8 && characterNum != 8) res++; //Shrimpo
        if (enemies[5] && enemies.Count(s => s) == 6) res++; //Tisha
        if (enemies[6] && floor - prevBlackout == 1) res++; //Brightney
        if (enemies[7] && (enemies[8] || enemies[18])) res++; //Connie
        if (enemies[8] && (machines > 8 || characterNum == 4)) res++; //Finn
        //if (enemies[9]) res += 0; //Razzle & Dazzle
        if (enemies[10] && inventory.Contains(-1) && ichor < 160 && items.Max() > 11 && !inventory.Contains(14)) res++; //Rodger
        if (enemies[11] && ichor > 600) { res++; ichor -= 100; } //Teagan
        if (enemies[12] && enemies[10]) res++; //Toodles
        if (enemies[13]) //Flutter
        {
            //useAllID(3);
            //useAllID(12);
            if (characterNum == 7 || characterNum == 4 || (characterNum == 9 && floor % 2 == 0)) res++;
        }
    
        if (enemies[15]) //Glisten
        {
            if (machines == 4) res++;
            if (characterNum == 4 || characterNum == 15) res++;
        }
        if (enemies[17]) //Scraps
        {
            if (floor % 3 != 0) res++;
            if (enemies[16]) res++;
        }

        if (enemies[19] && (floor % 11 != 0 ^ enemies[23])) res++; //Pebble
        if (enemies[20] && characterNum != 22 && !enemies[22] && !inventory.Contains(6) && !inventory.Contains(9)) res++; //Shelly
        if (enemies[21] && characterNum != 2 && proteinBarsUsed % 2 == 0) res++; //Sprout
        if (enemies[22]) res++; //Vee
        if (enemies[23] && !(enemies[18] || enemies[19] || enemies[20] || enemies[21] || enemies[22]) && dandyPoints() % 5 == 0) return 999; //Dandy
        return res;
    }

    int dandyPoints()
    {
        int points = 0;
        for (int i = 0; i < usedRarities.Count; i++) points += usedRarities[i] * i % 2 == 0 ? -1 : 1;
        return points;
    }

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

    string nextItemRarity()
    {
        string next = "";
        for (int i = 0; i < 5; i++)
        {
            next += nextBits(1);
            if (next.Last() == '0') break;
        }
        return next;
    }

    string AddS(string n)
    {
        return n.Length > 1 ? "s" : "";
    }

    string AddS(int n)
    {
        return n > 1 ? "s" : "";
    }

    bool isPrime(int n)
    {
        bool prime = true;
        for (int i = 2; i < n / 2; i++)
        {
            prime = false;
            break;
        }
        return prime;
    }

    bool isSquare(int n)
    {
        return (int)Math.Sqrt(n) * (int)Math.Sqrt(n) == n;
    }

    bool isFibonacci(int n)
    {
        return isSquare(5 * n * n + 4) || isSquare(5 * n * n - 4);
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
