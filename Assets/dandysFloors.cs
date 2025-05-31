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
    [SerializeField] List<KMSelectable> SubmissionKeypad;
    [SerializeField] TextMesh InputText;
    [SerializeField] List<Material> ModuleBGMaterials;
    [SerializeField] List<Material> ColorMaterials;
    [SerializeField] MeshRenderer ModuleRenderer;
    [SerializeField] KMSelectable DisplaySelectable;
    [SerializeField] TextMesh StrikeText;
    [SerializeField] AudioClip NextStageClip;
    [SerializeField] AudioClip DoorSlamClip;
    [SerializeField] AudioClip BlackoutClip;
    [SerializeField] AudioClip SolveClip;

    static int ModuleIdCounter = 1;
    int ModuleId;
    private bool ModuleSolved, DelayedSolve;
    private string[] ignoredModules;

    string[] ToonNames = new string[24] { "Boxten", "Cosmo", "Poppy", "Looey", "Shrimpo", "Tisha", "Brightney", "Connie", "Finn", "Razzle & Dazzle", "Rodger", "Teagan", "Toodles", "Flutter", "Gigi", "Glisten", "Goob", "Scraps", "Astro", "Pebble", "Shelly", "Sprout", "Vee", "Dandy" };
    string[] ItemNames = new string[16] { "Gumballs", "Chocolate", "Pop", "Speed Candy", "Protein Bar", "Stealth Candy", "Skill Check Candy", "Jumper Cable", "Bandage", "Enigma Candy", "Air Horn", "Bottle o' Pop", "Health Kit", "Box o' Chocolates", "Eject Button", "Smoke Bomb" };
    string[] EnemyRarityNames = new string[5] { "Common", "Uncommon", "Rare", "Main", "Lethal" };
    string[] ItemRarityNames = new string[5] { "Common", "Uncommon", "Rare", "Very Rare", "Ultra Rare" };
    int[] EnemyRaritiesIchor = new int[5] { 5, 6, 8, 10, 25 };
    int[] ItemRaritiesIchor = new int[5] { 1, 2, 3, 5, 10 };
    string base36 = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    string initSeed = "";
    string fullSeed = "";
    string usingSeed = "";

    float cooldown = 0; // Originally, this module was supposed to have a 10-second cooldown between stage generations, but this idea was scrapped before the module's release
    int lastSolves, curSolves = 0;
    bool inStages, inSubmission;
    bool done, detonating;
    string input = "";
    bool canStartRecovery;
    bool inRecoveryMode;

    int characterNum;
    int hp;
    int ichor = 0;
    int[] inventory = new int[3] { -1, -1, -1 };
    int[] itemUsages = new int[3] { 0, 0, 0 };
    List<int> usedRarities = new List<int>();
    List<int> floorItemsUsed = new List<int>();
    int proteinBarsUsed;
    int prevBlackout = -1;
    
    int floor = -1;
    int curRecoveryFloor = -1;
    List<int> machines = new List<int>();
    List<bool[]> enemies = new List<bool[]>();
    List<bool> blackouts = new List<bool>();
    List<int> items;
    int itemCount;
    bool unicorn;

    void Awake()
    {
        ModuleId = ModuleIdCounter++;
        DandyIcon.OnInteract += delegate () { DandyIconPress(); return false; };
        foreach (KMSelectable enemy in EnemySelectables) {
            enemy.OnInteract += delegate () { EnemyPress(enemy); return false; };
        }
        StageSubmitButton.OnInteract += delegate () { SubmitPress(); return false; };
        foreach (KMSelectable key in SubmissionKeypad) {
            key.OnInteract += delegate () { KeyPress(key); return false; };
        }
        DisplaySelectable.OnInteract += delegate () { DisplayPress(); return false; };
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
        if (!inRecoveryMode && !done)
        {
            GetComponent<KMBombModule>().HandleStrike();
            Log("You pressed the submit button when you shouldn't have. Strike!");
        }
        else EnterSubmissionMode();
    }

    void KeyPress(KMSelectable key)
    {
        StopCoroutine("FlashDisplay");
        DisplaySelectable.GetComponent<MeshRenderer>().material = ColorMaterials[1];
        key.AddInteractionPunch();
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, key.transform);
        int keyIdx = SubmissionKeypad.IndexOf(key);
        if (keyIdx < 10 && input.Length != 4) input += keyIdx.ToString();
        else if (keyIdx == 10) input = "";
        else if (keyIdx == 11)
        {
            if (input == ichor.ToString()) { StartCoroutine("Solve"); DelayedSolve = true; }
            else
            {
                Log("Inputted the incorrect number of ichor. Strike!");
                GetComponent<KMBombModule>().HandleStrike();
                if (Bomb.GetSolvableModuleNames().Where(a => !ignoredModules.Contains(a)).ToList().Count > 1)
                {
                    canStartRecovery = true;
                    StartCoroutine("FlashDisplay");
                }
                input = "";
            }
        }
        InputText.text = input;
    }

    void EnemyPress(KMSelectable key)
    {
        if (!inSubmission) return;
        key.AddInteractionPunch();
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, key.transform);
        curRecoveryFloor++;
        if (curRecoveryFloor > floor) EnterSubmissionMode();
        else StartCoroutine("DisplayStage", curRecoveryFloor);
    }

    void DisplayPress()
    {
        if (!canStartRecovery) return;
        DisplaySelectable.AddInteractionPunch();
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, DisplaySelectable.transform);
        StageMode.SetActive(true);
        foreach (KMSelectable enemy in EnemySelectables) enemy.gameObject.transform.Find("HL").gameObject.SetActive(true);
        canStartRecovery = false;
        inRecoveryMode = true;
        curRecoveryFloor = 0;
        StartCoroutine("DisplayStage", curRecoveryFloor);
        SubmissionMode.SetActive(false);
    }

    void EnterStageMode()
    {
        inStages = true;
        DandyIcon.gameObject.SetActive(false);
        StageMode.SetActive(true);
        foreach (KMSelectable enemy in EnemySelectables) enemy.gameObject.transform.Find("HL").gameObject.SetActive(false);
    }

    void EnterStrikeMode()
    {
        inSubmission = true;
        DandyIcon.gameObject.SetActive(false);
        StageMode.SetActive(false);
        StrikeMode.SetActive(true);
        StartCoroutine("StrikeStrikeStrike");
    }

    void EnterSubmissionMode()
    {
        Audio.PlaySoundAtTransform(DoorSlamClip.name, transform);
        ModuleRenderer.material = ModuleBGMaterials[0];
        inSubmission = true;
        DandyIcon.gameObject.SetActive(false);
        StageMode.SetActive(false);
        SubmissionMode.SetActive(true);
        if (!inRecoveryMode) Log($"Entering submission mode: correct answer - {ichor} ichor.");
        inRecoveryMode = false;
    }

    void GenerateStage()
    {
        if (inSubmission) return;
        if (floor > 0) 
        {
            if (blackouts[floor]) prevBlackout = floor;
        }
        floorItemsUsed = new List<int>();
        floor++;
        Log($"Floor #{floor + 1}:");

        if (floor >= 2)
        {
            if (Rnd.Range(0, 100) < Math.Min(30, 2 * floor + 1)) blackouts.Add(true);
            else blackouts.Add(false);
        }
        else blackouts.Add(false);
        Log($"There is {(!blackouts[floor] ? "not " : "")}a blackout this floor{(blackouts[floor] ? "!" : ".")}");

        GenerateEnemies();
        GenerateMachines();
        GenerateItems();
        PickUpItems();
        ProcessDamage();
        UseItems();
        if (hp <= 0)
        {
            Log("Player's HP is now 0 or lower, press the submit button on this floor and submit the answer.");
            done = true;
        }
        else Log($"Player's HP after this floor is {hp}.");

        StartCoroutine("DisplayStage", floor);
    }

    void GenerateEnemies()
    {
        int enemyCount = Math.Min(2 + (int)(floor + 1) / 10, 6);
        int idx = 0;
        int[] randomIdxs = Enumerable.Range(0, 24).ToArray().Shuffle();
        bool[] floorEnemies = new bool[24];
        while (floorEnemies.Count(e => e) != enemyCount)
        {
            int upper = GetEnemyProbability(GetEnemyRarity(randomIdxs[idx]));
            floorEnemies[randomIdxs[idx]] = Rnd.Range(0, upper) == 1;
            idx = (idx + 1) % 24;
        }
        enemies.Add(floorEnemies);
        List<string> floorEnemyNames = new List<string>();
        for (int i = 0; i < 24; i++) if (floorEnemies[i]) floorEnemyNames.Add(ToonNames[i]);
        Log($"Active enemies on this floor: {floorEnemyNames.Join(", ")}.");

        for (int i = 0; i < 24; i++)
        {
            if (floorEnemies[i])
            {
                int enemyRarity = GetEnemyRarity(i);
                ichor += EnemyRaritiesIchor[enemyRarity];
                if (i == characterNum) ichor += 40;
                Log($"Added {EnemyRaritiesIchor[enemyRarity]} ichor for a{(EnemyRarityNames[enemyRarity].First() == 'U' ? "n" : "")} {EnemyRarityNames[enemyRarity]} {ToonNames[i]}{(i == characterNum ? " and additional 40 ichor for matching the player's toon" : "")}. Player now has {ichor} ichor.");
            }
        }
    }

    void GenerateMachines()
    {
        int floorMachines = Rnd.Range(4, 11);
        machines.Add(floorMachines);
        if (enemies[floor][18])
        {
            int displayedMachines = floorMachines;
            floorMachines = (int)(displayedMachines * 1.25 + Bomb.GetBatteryCount());
            Log($"The displayed number of machines is {displayedMachines}. Because Astro is active, the real number of machines is {displayedMachines} * 1.25 + {Bomb.GetBatteryCount()} = {floorMachines}.");
        }
        else Log($"There are {machines[floor]} machines on this floor.");
        ichor += 5 * floorMachines;
        Log($"Added {5 * floorMachines} ichor from the {floorMachines} machines. Player now has {ichor} ichor.");
    }

    void GenerateItems()
    {
        Log("Generating items:");
        string itemsBits = NextBits(2);
        itemCount = Convert.ToInt32(itemsBits, 2) + 1;
        Log($"Requesting 2 bits: {itemsBits}. There {(itemCount > 1 ? "are" : "is")} {itemCount} item{AddS(itemCount)} on this floor:");
        items = new List<int>();
        for (int i = 0; i < itemCount; i++)
        {
            string rarityBits = NextItemRarity();
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
            string itemBits = rarity == 4 ? NextBits(1) : NextBits(2);
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
            Log($"Requesting {rarityBits.Length} bit{AddS(rarityBits)}: {rarityBits}. Item rarity - {ItemRarityNames[rarity]}. Requesting {itemBits.Length} more bit{AddS(itemBits)}: {itemBits}. Item #{i + 1} is {ItemNames[item]}.");
        }
    }

    void PickUpItems()
    {
        Log("Picking up all the items:");
        for (int i = 0; i < itemCount; i++)
        {
            int pickUpIdx = 0;
            int item = items[i];
            if (inventory.Contains(-1))
            {
                pickUpIdx = inventory.IndexOf(x => x == -1);
                inventory[pickUpIdx] = item;
                itemUsages[pickUpIdx] = item == 13 ? 5 : 1;
                Log($"The inventory is not fully filled - putting {ItemNames[item]} in slot #{pickUpIdx + 1}.");
            }
            else UseThenPickup(item);
        }
        Log($"Inventory after picking up the items - {inventory.Select(x => x == -1 ? "Empty" : ItemNames[x]).Join(", ")}.");
    }

    void UseItems()
    {
        Log("Using items in player's inventory:");
        for (int i = 0; i < 3; i++)
        {
            if (inventory[i] != -1) TryToUseItem(i);
        }
        Log($"Inventory after using items - {inventory.Select(x => x == -1 ? "Empty" : ItemNames[x]).Join(", ")}.");
        if (characterNum < 18)
        {
            if (hp == 3 && inventory[0] == 12 && inventory[1] == 12 && inventory[2] == 12)
            {
                Log($"Player is not a main character, has 3 HP and 3 Health kits - submit 9999 ichor as an answer!");
                done = true;
                unicorn = true;
                ichor = 9999;
            }
        }
        else
        {
            if (hp == 2 && (inventory[0] == 8 || inventory[0] == 12) && (inventory[1] == 8 || inventory[1] == 12) && (inventory[2] == 8 || inventory[2] == 12))
            {
                Log($"Player is a main character, has 2 HP and 3 Bandages/Health kits - submit 4999 ichor as an answer!");
                done = true;
                unicorn = true;
                ichor = 4999;
            }
        }
        if (unicorn)
        {
            StrikeText.text = "UNICORN NOT\nACTIVATED!";
            StrikeText.fontSize = 170;
        }
    }

    void UseThenPickup(int item)
    {
        int itemRarity = GetItemRarity(item);
        string usedItem = "";
        int[] inventoryRarities = inventory.Select(x => GetItemRarity(x)).ToArray();
        if (inventoryRarities.Min() >= itemRarity) Log($"Skipping over {ItemNames[item]} as its rarity is not higher than any of the inventory's items' rarities.");
        else
        {
            for (int i = 2; i > -1; i--)
            {
                if (GetItemRarity(inventory[i]) == inventoryRarities.Min())
                {
                    usedItem = ItemNames[inventory[i]];
                    FullyUseItem(i);
                    inventory[i] = item;
                    itemUsages[i] = item == 13 ? 5 : 1;
                    Log($"Using {usedItem} from slot #{i + 1} to pick up {ItemNames[item]}. Player now has {ichor} ichor.");
                    break;
                }
            }
        }
    }

    void FullyUseItem(int slot)
    {
        ichor += ItemRaritiesIchor[GetItemRarity(inventory[slot])];
        if (inventory[slot] == 8) usedRarities.Add(2);
        else if (inventory[slot] == 12) usedRarities.Add(3);
        else usedRarities.Add(GetItemRarity(inventory[slot]));
        if (inventory[slot] == 4) proteinBarsUsed++;
        floorItemsUsed.Add(inventory[slot]);
        inventory[slot] = -1;
        itemUsages[slot] = 0;
    }

    void UseItemOnce(int slot)
    {
        itemUsages[slot]--;
        if (itemUsages[slot] == 0)
        {
            if (inventory[slot] == 8)
            {
                ichor += ItemRaritiesIchor[2];
                usedRarities.Add(2);
                hp++;
            }
            else if (inventory[slot] == 12)
            {
                ichor += ItemRaritiesIchor[3];
                usedRarities.Add(3);
                hp = characterNum < 18 ? 3 : 2;
            }
            else
            {
                ichor += ItemRaritiesIchor[GetItemRarity(inventory[slot])];
                usedRarities.Add(GetItemRarity(inventory[slot]));
            }
            if (inventory[slot] == 4) proteinBarsUsed++;
            Log($"Using {ItemNames[inventory[slot]]} from slot #{slot + 1}. Player now has {ichor} ichor.");
            if (inventory[slot] == 8 || inventory[slot] == 12) Log($"Player now has {hp} HP.");
            floorItemsUsed.Add(inventory[slot]);
            inventory[slot] = -1;
        }
        else
        {
            Log($"Using {ItemNames[inventory[slot]]} from slot #{slot + 1} once. This item now has {itemUsages[slot]} usages left.");
            floorItemsUsed.Add(inventory[slot]);
        }
    }

    void TryToUseItem(int slot)
    {
        if (CanUseItem(inventory[slot])) UseItemOnce(slot);
        else Log($"Unable to use the {ItemNames[inventory[slot]]} from slot #{slot + 1}. Skipping over it.");
    }

    void Start()
    {
        StageMode.gameObject.SetActive(false);
        StrikeMode.gameObject.SetActive(false);
        SubmissionMode.gameObject.SetActive(false);
        if (ignoredModules == null)
        {

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
            if (!ignoredModules.Contains("Dandy's Floors"))
            {
                ignoredModules = ignoredModules.ToList().Concat(new List<string> { "Dandy's Floors" }).ToArray();
            }
        }

        if (Bomb.GetSolvableModuleNames().Where(a => !ignoredModules.Contains(a)).ToList().Count < 2) 
        {
            Log("Not enough modules to generate even a single stage!");
            EnterSubmissionMode();
        }
        else CalculateSeed();
    }

    void Update()
    {
        if (!inSubmission)
        {
            int curSolves = Bomb.GetSolvedModuleNames().Where(a => !ignoredModules.Contains(a)).ToList().Count;
            if (curSolves > lastSolves && done)
            {
                if (!TwitchPlaysActive) EnterStrikeMode();
                else
                {
                    Log("You were supposed to press the submit button, but you didn't! Giving a strike and entering Submission Mode...");
                    GetComponent<KMBombModule>().HandleStrike();
                    done = true;
                    EnterSubmissionMode();
                }
            }
            else if (curSolves == Bomb.GetSolvableModuleNames().Where(a => !ignoredModules.Contains(a)).ToList().Count && curSolves > 0) EnterSubmissionMode();
            else
            {
                /*
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
                        cooldown = 10f;
                    }
                    lastSolves = curSolves;
                }
                */

                if (curSolves > lastSolves)
                {
                    if (!inStages)
                    {
                        EnterStageMode();
                        inStages = true;
                    }
                    GenerateStage();
                }
                lastSolves = curSolves;
            }
        }
    }

    void CalculateSeed()
    {
        Log("Generating the seed:");

        int SNLen = Bomb.GetSerialNumber().Length;
        if (SNLen <= 6)
        {
            for (int i = 0; i < 6; i++)
            {
                string shiftSN = Bomb.GetSerialNumber().Substring(i, 6 - i) + Bomb.GetSerialNumber().Substring(0, i);
                string convSN = Base36ToBinary(shiftSN);
                Log($"{shiftSN} into binary: {convSN} ({convSN.Length} bit{AddS(convSN)}{(convSN.Length > 31 ? ", which is too long" : "")})");
                if (convSN.Length <= 31)
                {
                    initSeed = convSN.Substring(1);
                    fullSeed = initSeed;
                    break;
                }
            }
        }
        if (initSeed == "" || SNLen > 6)
        {
            Log("Could not generate the run's seed - submit 0 ichor!");
            StrikeText.text = "SEED\nNOT\nFOUND!";
            StrikeText.fontSize = 250;
            done = true;
        }
        else
        {
            Log($"Initial seed: {fullSeed}.");

            Log($"Generating the rest of the seed:");
            string curSN = Bomb.GetSerialNumber();
            for (int i = 1; i < 36; i++)
            {
                curSN = Base36Caesar(curSN);
                string newBits = Base36ToBinary(curSN);
                if (newBits.Length < initSeed.Length) newBits = new string('0', initSeed.Length - newBits.Length) + newBits;
                string truncatedBits = newBits.Substring(newBits.Length - initSeed.Length, initSeed.Length);
                string xor = Convert.ToString(Convert.ToInt32(initSeed, 2) ^ Convert.ToInt32(truncatedBits, 2), 2);
                if (xor.Length < initSeed.Length) xor = new string('0', initSeed.Length - xor.Length) + xor;
                Log($"Iteration {i}: {curSN} -> {newBits} -> {truncatedBits} XOR {initSeed} -> {xor}");
                fullSeed += xor;
            }
            Log($"Full seed: {fullSeed}.");
            usingSeed = fullSeed;

            string charBits = NextBits(23);
            characterNum = Convert.ToInt32(charBits, 2) % 23;
            hp = characterNum >= 18 ? 2 : 3;
            Log($"Requesting 23 bits: {charBits}. You are playing as Toon number {Convert.ToInt32(charBits, 2)} % 23 - {ToonNames[characterNum]}. You start with {hp} HP.");
        }
    }

    void ProcessDamage()
    {
        Log("Processing Damage:");
        bool takenDamage = false;
        if (enemies[floor][16]) //Goob
        { 
            if (!isPrime(floor + 1) && floor != 1)
            {
                Log("Goob: Floor number is composite - Goob deals 1 damage:");
                TakeDamage(1);
                takenDamage = true;
            }
            else if (enemies[floor][23])
            {
                Log("Goob: Floor number is prime and Dandy is present - Goob deals 3 damage!");
                TakeDamage(3);
                takenDamage = true;
            }
            else
            {
                Log("Goob: Floor number is prime and Dandy is not present - other enemies' rules are not checked!");
                return;
            }
        }
        if (enemies[floor][0] && isPrime(floor + 1)) //Boxten
        {
            Log("Boxten: Floor number is prime - Boxten deals 1 damage.");
            TakeDamage(1);
            takenDamage = true;
        }
        if (enemies[floor][1] && (enemies[floor][21] || enemies[floor][23]))  //Cosmo
        {
            Log("Cosmo: Dandy/Sprout is present - Cosmo deals 1 damage.");
            TakeDamage(1);
            takenDamage = true;
        }
        if (enemies[floor][2] && (isFibonacci(floor + 1)) && (floor > 6))  //Poppy
        {
            Log("Poppy: Floor number is present in the Fibonacci sequence and greater than 7 - Poppy deals 1 damage.");
            TakeDamage(1);
            takenDamage = true;
        }
        if (enemies[floor][3] && inventory.Contains(10)) //Looey
        {
            Log("Looey: Player has an Air Horn in their inventory - Looey deals 1 damage. The Air Horn is used immediately.");
            TakeDamage(1);
            takenDamage = true;
            UseItemOnce(Array.LastIndexOf(inventory, 10));
        }
        if (enemies[floor][4] && machines[floor] > 8 && characterNum != 8) //Shrimpo
        {
            Log("Shrimpo: There are more than 8 machines and the player is not Finn - Shrimpo deals 1 damage.");
            TakeDamage(1);
            takenDamage = true;
        }
        if (enemies[floor][5] && enemies[floor].Count(s => s) == 6) //Tisha
        {
            Log("Tisha: There are exactly 6 enemies on the floor - Tisha deals 1 damage.");
            TakeDamage(1);
            takenDamage = true;
        }
        if (enemies[floor][6] && (floor > 0) && (floor - prevBlackout == 1)) //Brightney
        {
            Log("Brightney: There was a blackout on the previous floor - Brightney deals 1 damage.");
            TakeDamage(1);
            takenDamage = true;
        }
        if (enemies[floor][7] && (enemies[floor][8] || enemies[floor][18])) //Connie
        {
            Log("Connie: Finn/Astro is present - Connie deals 1 damage.");
            TakeDamage(1);
            takenDamage = true;
        }
        if (enemies[floor][8] && (machines[floor] > 8 || characterNum == 4)) //Finn
        {
            Log("Finn: There are more than 8 machines or the player is Shrimpo - Finn deals 1 damage.");
            TakeDamage(1);
            takenDamage = true;
        }
        // if (enemies[floor][9]) Log("Razzle & Dazzle: Razzle & Dazzle doesn't deal damage."); //Razzle & Dazzle
        if (enemies[floor][10] && inventory.Contains(-1) && ichor < 160 && items.Max() > 11 && !inventory.Contains(14)) //Rodger
        {
            Log("Rodger: Player's inventory is not completely full and doesn't contain an Eject Button, player has less than 160 ichor and at least one item on the floor is Very/Ultra Rare - Rodger deals 1 damage.");
            TakeDamage(1);
            takenDamage = true;
        }
        if (enemies[floor][11] && ichor > 600) //Teagan
        {
            Log("Teagan: Player has more than 600 ichor - Teagan deals 1 damage. 100 ichor is subtracted.");
            TakeDamage(1);
            takenDamage = true;
            ichor -= 100;
        }
        if (enemies[floor][12] && enemies[floor][10]) //Toodles
        {
            Log("Toodles: Rodger is present - Toodles deals 1 damage.");
            TakeDamage(1);
            takenDamage = true;
        }
        if (enemies[floor][13]) //Flutter
        {
            if (characterNum == 7 || characterNum == 4 || (characterNum == 9 && (floor + 1) % 2 == 0))
            {
                Log("Flutter: Player is Connie/Shrimpo or Razzle & Dazzle on an even floor - Flutter deals 1 damage.");
                TakeDamage(1);
            }
            Log($"Flutter: All Pops and Bottles o' Pop are used immediately.");
            for (int i = 0; i < 3; i++)
            {
                if (inventory[i] == 2 || inventory[i] == 11) UseItemOnce(i);
            }
        }
    
        if (enemies[floor][15]) //Glisten
        {
            bool glistenDamage = false;
            if (machines[floor] == 4)
            {
                Log("Glisten: There are 4 machines on the floor - Glisten deals 1 damage.");
                TakeDamage(1);
                takenDamage = true;
                glistenDamage = true;
            }
            if (characterNum == 4 || characterNum == 15)
            {
                Log($"Glisten: Player is Shrimpo/Glisten - Glisten deals 1 {(glistenDamage ? "additional " : "")}damage.");
                TakeDamage(1);
                takenDamage = true;
            }
        }
        if (enemies[floor][17]) //Scraps
        {
            bool scrapsDamage = false;
            if ((floor + 1) % 3 != 0)
            {
                Log("Scraps: Floor number is not divisible by 3 - Scraps deals 1 damage.");
                TakeDamage(1);
                takenDamage = true;
                scrapsDamage = true;
            }
            if (enemies[floor][16])
            {
                Log($"Scraps: Goob is present - Scraps deals 1 {(scrapsDamage ? "additional " : "")}damage.");
                TakeDamage(1);
                takenDamage = true;
            }
        }
        if (enemies[floor][18] && (machines[floor] > 7) && (machines[floor] % 4 == 0)) //Astro
        {
            Log("Astro: Number of machines is divisible by 4 and greater than 7 - Astro deals 1 damage.");
            TakeDamage(1);
            takenDamage = true;
        }
        if (enemies[floor][19] && ((floor + 1) % 11 != 0 ^ enemies[floor][23])) //Pebble
        {
            if (enemies[floor][23])
            {
                Log("Pebble: Dandy is present and floor number is divisible by 11 - Pebble deals 1 damage.");
            }
            else
            {
                Log("Pebble: Dandy is not present and floor number is not divisible by 11 - Pebble deals 1 damage.");
            }
            TakeDamage(1);
            takenDamage = true;
        }
        if (enemies[floor][20] && characterNum != 22 && !enemies[floor][22] && !inventory.Contains(6) && !inventory.Contains(9)) //Shelly
        {
            Log("Shelly: Vee is not present, player is not Vee and they don't have a Skill Check Candy nor Enigma Candy in their inventory - Shelly deals 1 damage.");
            TakeDamage(1);
            takenDamage = true;
        }
        if (enemies[floor][21] && characterNum != 2 && proteinBarsUsed % 2 == 0) //Sprout
        {
            Log("Sprout: Player is not Cosmo and the number of Protein Bars throughout the module is even - Sprout deals 1 damage.");
            TakeDamage(1);
            takenDamage = true;
        }
        if (enemies[floor][22]) //Vee
        {
            Log("Vee: Vee deals 1 damage.");
            TakeDamage(1);
            takenDamage = true;
        }
        if (enemies[floor][23] && !(enemies[floor][18] || enemies[floor][19] || enemies[floor][20] || enemies[floor][21] || enemies[floor][22]) && DandyPoints() % 5 == 0) //Dandy
        {
            Log($"Dandy: There are no main enemies and the score obtained from the rules ({DandyPoints()}) is divisible by 5 - Dandy kills the player!");
            hp = 0;
            done = true;
            takenDamage = true;
        }
        if (enemies[floor][14]) //Gigi
        {
            if (inventory.Count(i => i == -1) == 3)
            {
                Log("Gigi: Player's inventory is empty - Gigi deals 1 damage.");
                TakeDamage(1);
                takenDamage = true;
            }
            else
            {
                Log($"Gigi: Player's inventory is not empty - Gigi steals the item in slot {inventory.IndexOf(i => i != -1) + 1}.");
                inventory[inventory.IndexOf(i => i != -1)] = -1;
            }
        }
        if (!takenDamage) Log("No damage was dealt by any of the enemies' rules!");
    }

    void TakeDamage(int n)
    {
        for (int i = 0; i < n; i++)
        {
            hp--;
            if (n > 1) Log($"Taking damage {i + 1} of {n}. Player now has {hp} HP.");
            else Log($"Taking 1 damage. Player now has {hp} HP.");
            for (int s = 2; s > -1; s--)
            {
                if (inventory[s] == 8 || inventory[s] == 12)
                {
                    if (ShouldHeal(inventory[s] == 8)) UseItemOnce(s);
                }
            }
        }
    }

    int DandyPoints()
    {
        int points = 0;
        for (int i = 0; i < usedRarities.Count; i++) points += usedRarities[i] * (i % 2 == 0 ? -1 : 1);
        return points;
    }

    string Base36ToBinary(string n)
    {
        int dec = 0;
        for (int i = 0; i < n.Length; i++)
        {
            dec += base36.IndexOf(n[i]) * (int)Math.Pow(36, n.Length - i - 1);
        }
        return Convert.ToString(dec, 2);
    }

    string Base36Caesar(string n)
    {
        string caesar = "";
        foreach (char c in n)
        {
            caesar += base36[(base36.IndexOf(c) + 1) % 36];
        }
        return caesar;
    }

    string NextBits(int n)
    {
        if (usingSeed.Length < n) usingSeed += fullSeed;
        string next = usingSeed.Substring(0, n);
        usingSeed = usingSeed.Substring(n);
        return next;
    }

    string NextItemRarity()
    {
        string next = "";
        for (int i = 0; i < 5; i++)
        {
            next += NextBits(1);
            if (next.Last() == '0') break;
        }
        return next;
    }

    int GetEnemyRarity(int e)
    {
        return e < 6 ? 0 : (e < 13 ? 1 : (e < 18 ? 2 : (e != 23 ? 3 : 4)));
    }

    int GetItemRarity(int i)
    {
        if (i == 8 || i == 12) return 5;
        return i < 3 ? 0 : (i < 7 ? 1 : (i < 11 ? 2 : (i < 14 ? 3 : 4)));
    }

    int GetEnemyProbability(int e)
    {
        switch (e)
        {
            case 0:
                return 2;
            case 1:
                return Math.Max(2, (int)Math.Round(Mathf.Lerp(3, 2, floor / 19)));
            case 2:
                return Math.Max(10, (int)Math.Round(Mathf.Lerp(20, 10, floor / 19)));
            case 3:
                return Math.Max(25, (int)Math.Round(Mathf.Lerp(50, 25, floor / 19)));
            case 4:
                return (floor + 1) % 6 == 0 ? 1 : (floor > 5 ? 2 : 0);
            default:
                return 0;
        }
    }

    bool CanUseItem (int i)
    {
        switch (i)
        {
            case 0: return true; //Gumballs
            case 1: return (enemies[floor][6] || enemies[floor][7] || enemies[floor][8] || enemies[floor][9] || enemies[floor][10] || enemies[floor][11] || enemies[floor][12]) && !floorItemsUsed.Contains(1); //Chocolate
            case 2: return (enemies[floor][6] || enemies[floor][7] || enemies[floor][8] || enemies[floor][9] || enemies[floor][10] || enemies[floor][11] || enemies[floor][12]) && !floorItemsUsed.Contains(2); //Pop
            case 3: return (enemies[floor][11] || enemies[floor][12] || enemies[floor][13] || enemies[floor][14] || enemies[floor][18] || enemies[floor][19] || enemies[floor][20] || enemies[floor][21]) && !floorItemsUsed.Contains(3); //Speed candy
            case 4: return new bool[8] { enemies[floor][11], enemies[floor][12], enemies[floor][13], enemies[floor][14], enemies[floor][18], enemies[floor][19], enemies[floor][20], enemies[floor][21] }.Count(e => e) >= 2 && !floorItemsUsed.Contains(4); //Protein bar
            case 5: return (enemies[floor][18] || enemies[floor][19] || enemies[floor][20] || enemies[floor][21] || enemies[floor][22]) && !floorItemsUsed.Contains(5); //Stealth candy
            case 6: return true; //Skill Check candy
            case 7: return true; //Jumper cable
            case 8: return ShouldHeal(true); //Bandage
            case 9: return !floorItemsUsed.Contains(9); //Enigma candy
            case 10: return true; //Air horn
            case 11: return (enemies[floor][6] || enemies[floor][7] || enemies[floor][8] || enemies[floor][9] || enemies[floor][10] || enemies[floor][11] || enemies[floor][12]) && !floorItemsUsed.Contains(11); //Bottle o' Pop
            case 12: return ShouldHeal(false); //Health kit
            case 13: return (enemies[floor][6] || enemies[floor][7] || enemies[floor][8] || enemies[floor][9] || enemies[floor][10] || enemies[floor][11] || enemies[floor][12]) && !floorItemsUsed.Contains(13); //Bob o' Chocolates
            case 14: return enemies[floor][10] && !floorItemsUsed.Contains(14); //Eject button
            case 15: return enemies[floor].Count(e => e) > 4 && !floorItemsUsed.Contains(15); //Smoke bomb
            default: return false;
        }
    }

    bool ShouldHeal(bool bandage)
    {
        if (characterNum < 18) 
        {
            if (bandage) return hp == 2 || hp == 1;
            else return hp == 1 && !inventory.Contains(8);
        }
        else return hp == 1;
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
        if (n == 1) return false;
        bool prime = true;
        for (int i = 2; i < (int)Math.Sqrt(n) + 1; i++)
        {
            if (n % i == 0)
            {
                prime = false;
                break;
            }
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

    IEnumerator DisplayStage(int displayFloor)
    {
        bool doIntro = !inSubmission;
        ModuleRenderer.material = ModuleBGMaterials[0];
        for (int i = 0; i < 24; i++)
        {
            EnemyRenderers[i].transform.parent.GetComponent<MeshRenderer>().material = ColorMaterials[1];
            EnemyRenderers[i].material = EnemySilhouetteMaterials[i];
        }
        if (doIntro)
        {
            Audio.PlaySoundAtTransform(NextStageClip.name, transform);
            for (int i = 0; i < 3; i++)
            {
                FloorText.text = new string('.', i + 1);
                MachinesText.text = new string('.', i + 1);
                yield return new WaitForSeconds(2.1f / 3f);
            }
        }

        for (int i = 0; i < 24; i++) EnemyRenderers[i].material = enemies[displayFloor][i] ? EnemyMaterials[i] : EnemySilhouetteMaterials[i];
        FloorText.text = (displayFloor + 1).ToString();
        MachinesText.text = machines[displayFloor].ToString();

        if (doIntro) yield return new WaitForSeconds(1.5f);
        if (blackouts[displayFloor])
        {
            Audio.PlaySoundAtTransform(BlackoutClip.name, transform);
            ModuleRenderer.material = ModuleBGMaterials[1];
            for (int i = 0; i < 24; i++) EnemyRenderers[i].transform.parent.GetComponent<MeshRenderer>().material = ColorMaterials[0];
        }
    }

    IEnumerator FlashDisplay()
    {
        while (canStartRecovery)
        {
            DisplaySelectable.GetComponent<MeshRenderer>().material = ColorMaterials[1];
            yield return new WaitForSeconds(0.5f);
            DisplaySelectable.GetComponent<MeshRenderer>().material = ColorMaterials[2];
            yield return new WaitForSeconds(0.5f);
        }
        DisplaySelectable.GetComponent<MeshRenderer>().material = ColorMaterials[1];
    }

    IEnumerator StrikeStrikeStrike()
    {
        Log("You were supposed to press the submit button, but you didn't! Detonating the bomb...");
        detonating = true;
        while (enabled && !ModuleSolved)
        {
            GetComponent<KMBombModule>().HandleStrike();
            yield return new WaitForSeconds(2 / 3f);
        }
    }

    IEnumerator Solve()
    {
        SubmissionMode.SetActive(false);
        DandyIcon.gameObject.SetActive(true);
        Audio.PlaySoundAtTransform(SolveClip.name, transform);
        yield return new WaitForSeconds(2.6f);
        Log("Inputted the correct number of ichor. Module solved!");
        GetComponent<KMBombModule>().HandlePass();
        ModuleSolved = true;
    }

#pragma warning disable 414
    private readonly string TwitchHelpMessage = @"Use <!{0} flower> to press the flower button in the module's initial state. Use <!{0} submit> to press the submit button during stages. Use <!{0} submit #> to submit an amount of ichor during submission mode. Use <!{0} recovery> to enter recovery mode, and use <!{0} next> to progress stages in recovery mode.";
    bool TwitchPlaysActive;
#pragma warning restore 414
    IEnumerator ProcessTwitchCommand(string Command)
    {
        var Args = Command.ToLowerInvariant().Split(new[] { " " }, StringSplitOptions.RemoveEmptyEntries);
        if (Args.Length == 0) yield break;
        else
        {
            switch (Args[0])
            {
                case "flower":
                    if (inStages || inSubmission) yield return "sendtochaterror Unable to press the flower button!";
                    else
                    {
                        yield return null;
                        DandyIcon.OnInteract();
                    }
                    break;
                case "submit":
                    if (Args.Length == 2 && !inSubmission) yield return "sendtochaterror Unable to submit an answer not in submission mode!";
                    else if (Args.Length != 2 && inSubmission) yield return "sendtochaterror Didn't provide an answer to submit!";
                    else
                    {
                        if (inSubmission)
                        {
                            yield return null;
                            foreach (char c in Args[1])
                            {
                                SubmissionKeypad[c - '0'].OnInteract();
                                yield return new WaitForSeconds(0.1f);
                            }
                            SubmissionKeypad[11].OnInteract();
                            if (DelayedSolve)
                            {
                                yield return "solve";
                                yield return string.Format("awardpointsonsolve {0}", 3 * (floor + 1));
                            }
                        }
                        else
                        {
                            yield return null;
                            StageSubmitButton.OnInteract();
                        }
                    }
                    break;
                case "recovery":
                    if (!inStages || !canStartRecovery) yield return "sendtochaterror Unable to enter recovery mode now!";
                    else
                    {
                        yield return null;
                        DisplaySelectable.OnInteract();
                    }
                    break;
                case "next":
                    if (!inRecoveryMode) yield return "sendtochaterror Cannot progress stages not in recovery mode!";
                    else
                    {
                        yield return null;
                        EnemySelectables[0].OnInteract();
                    }
                    break;
                default:
                    yield return "sendtochaterror Invalid command!";
                    break;
            }
        }
    }

    IEnumerator TwitchHandleForcedSolve()
    {
        StopCoroutine("StrikeStrikeStrike");
        StrikeMode.SetActive(false);
        SubmissionMode.SetActive(true);
        if (!inSubmission)
        {
            done = true;
            EnterSubmissionMode();
        }
        SubmissionKeypad[10].OnInteract();
        yield return new WaitForSeconds(0.1f);
        foreach (char c in ichor.ToString())
        {
            SubmissionKeypad[c - '0'].OnInteract();
            yield return new WaitForSeconds(0.1f);
        }
        SubmissionKeypad[11].OnInteract();
        yield return new WaitForSeconds(SolveClip.length);
    }
}