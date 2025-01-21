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

    static int ModuleIdCounter = 1;
    int ModuleId;
    private bool ModuleSolved;
    private string[] ignoredModules;

    string[] ToonNames = new string[24] { "Boxten", "Cosmo", "Poppy", "Looey", "Shrimpo", "Tisha", "Brightney", "Connie", "Finn", "Razzle & Dazzle", "Rodger", "Teagan", "Toodles", "Flutter", "Gigi", "Glisten", "Goob", "Scraps", "Astro", "Pebble", "Shelly", "Sprout", "Vee", "Dandy" };
    string[] ItemNames = new string[16] { "Gumballs", "Chocolate", "Pop", "Speed Candy", "Protein Bar", "Stealth Candy", "Skill Check Candy", "Jumper Cable", "Bandage", "Enigma Candy", "Air Horn", "Bottle o' Pop", "Health Kit", "Box o' Chocolates", "Eject Button", "Smoke Bomb" };
    string[] EnemyRarityNames = new string[5] { "Common", "Uncommon", "Rare", "Main", "Lethal" };
    string[] ItemRarityNames = new string[5] { "Common", "Uncommon", "Rare", "Very Rare", "Ultra Rare" };
    int[] EnemyRaritiesIchor = new int[5] { 5, 6, 8, 10, 25 };
    int[] ItemRaritiesIchor = new int[5] { 1, 2, 3, 5, 10 };
    int[] EnemyChances = new int[5] { 2, 3, 5, 8, 10 };
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
    int[] itemUsages = new int[3] { 0, 0, 0 };
    List<int> usedRarities = new List<int>();
    int proteinBarsUsed;
    bool detonating;

    int floor, machines;
    bool[] enemies = new bool[24];
    int itemCount;
    List<int> items;
    int prevBlackout = -1;
    string input = "";

    void Awake()
    {
        ModuleId = ModuleIdCounter++;
        GetComponent<KMBombModule>().OnActivate += delegate () { canStart = true; };
        DandyIcon.OnInteract += delegate () { DandyIconPress(); return false; };
        foreach (KMSelectable enemy in EnemySelectables) {
            enemy.gameObject.transform.Find("HL").gameObject.SetActive(false);
        }
        StageSubmitButton.OnInteract += delegate () { SubmitPress(); return false; };
        foreach (KMSelectable key in SubmissionKeypad) {
            key.OnInteract += delegate () { KeyPress(key); return false; };
        }
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

    void KeyPress(KMSelectable key)
    {
        key.AddInteractionPunch();
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, key.transform);
        int keyIdx = SubmissionKeypad.IndexOf(key);
        if (keyIdx < 10 && input.Length != 4) input += keyIdx.ToString();
        else if (keyIdx == 10 && input.Length > 0) input = input.Substring(0, input.Length - 1);
        else if (keyIdx == 11)
        {
            if (input == ichor.ToString())
            {
                Log("Inputted the correct number of ichor. Module solved!");
                GetComponent<KMBombModule>().HandlePass();
                ModuleSolved = true;
                SubmissionMode.SetActive(false);
                DandyIcon.gameObject.SetActive(true);
            }
            else
            {
                Log("Inputted the incorrect number of ichor. Strike!");
                GetComponent<KMBombModule>().HandleStrike();
            }
        }
        InputText.text = input;
    }

    void EnterStageMode()
    {
        inStages = true;
        DandyIcon.gameObject.SetActive(false);
        StageMode.SetActive(true);
    }

    void EnterStrikeMode()
    {
        inSubmission = true;
        StageMode.SetActive(false);
        StrikeMode.SetActive(true);
        StartCoroutine("StrikeStrikeStrike");
    }

    void EnterSubmissionMode()
    {
        inSubmission = true;
        DandyIcon.gameObject.SetActive(false);
        StageMode.SetActive(false);
        SubmissionMode.SetActive(true);
        Log($"Entering submission mode: correct answer - {ichor} ichor.");
    }

    void GenerateStage()
    {
        floor++;
        FloorText.text = floor.ToString();
        Log($"Floor #{floor}:");

        GenerateEnemies();
        GenerateMachines();
        GenerateItems();
        PickUpItems();
        int damage = CalculateDamage();
        if (damage == 0) Log("No damage was dealt by any of the rules!");
    }

    void GenerateEnemies()
    {
        int enemyCount = Math.Min(2 + (int)floor / 10, 6);
        int idx = 0;
        enemies = new bool[24];
        while (enemies.Count(e => e) != enemyCount)
        {
            int upper = EnemyChances[GetEnemyRarity(idx)];
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
        for (int i = 0; i < 24; i++)
        {
            if (enemies[i])
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
        machines = Rnd.Range(4, 11);
        MachinesText.text = machines.ToString();
        if (enemies[18])
        {
            machines = (int)(machines * 1.25 + Bomb.GetBatteryCount());
            Log($"The displayed number of machines is {MachinesText.text}. Because Astro is active, the real number of machines is {MachinesText.text} * 1.25 + {Bomb.GetBatteryCount()} = {machines}.");
        }
        else Log($"There are {machines} machines on this floor.");
        ichor += 5 * machines;
        Log($"Added {5 * machines} ichor from the machines. Player now has {ichor} ichor.");
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

    void UseThenPickup(int item)
    {
        int itemRarity = GetItemRarity(item);
        string usedItem = "";
        for (int i = 2; i > -1; i--)
        {
            int rarity = GetItemRarity(inventory[i]);
            if (rarity < itemRarity)
            {
                usedItem = ItemNames[inventory[i]];
                FullyUseItem(i);
                inventory[i] = item;
                itemUsages[i] = item == 13 ? 5 : 1;
                Log($"Using {usedItem} from slot #{i + 1} to pick up {ItemNames[item]}. Player now has {ichor} ichor.");
                break;
            }
        }
        if (usedItem == "") Log($"Skipping over {ItemNames[item]} as its rarity is not higher than any of the inventory's items' rarities.");
    }

    void FullyUseItem(int slot)
    {
        ichor += ItemRaritiesIchor[GetItemRarity(inventory[slot])];
        inventory[slot] = -1;
        itemUsages[slot] = 0;
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
    }

    void Update()
    {
        if (canStart && !inSubmission)
        {
            int curSolves = Bomb.GetSolvedModuleNames().Where(a => !ignoredModules.Contains(a)).ToList().Count;
            if (curSolves == Bomb.GetModuleNames().Where(a => !ignoredModules.Contains(a)).ToList().Count) EnterSubmissionMode();
            if (curSolves > lastSolves && done) EnterStrikeMode();
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
            string convSN = Base36ToBinary(shiftSN);
            Log($"{shiftSN} into binary: {convSN} ({convSN.Length} bit{AddS(convSN)}{(convSN.Length > 31 ? ", which is too long" : "")})");
            if (convSN.Length <= 31)
            {
                initSeed = convSN.Substring(1);
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
            Log($"Requesting 23 bits: {charBits}. You are playing as Toon number {Convert.ToInt32(charBits, 2)} % 23 + 1 - {ToonNames[characterNum]}. You start with {hp} HP.");
        }
    }

    int CalculateDamage()
    {
        Log("Processing Damage:");
        int res = 0;
        if (enemies[16]) //Goob
        { 
            if (!isPrime(floor) && floor != 1)
            {
                Log("Goob: Floor number is composite - Goob deals 1 damage.");
                res++;
            }
            else if (enemies[23])
            {
                Log("Goob: Floor number is prime and Dandy is present - Goob deals 3 damage!");
                res += 3;
            }
            else
            {
                Log("Goob: Floor number is prime and Dandy is not present - other enemies' rules are not checked!");
                return 0;
            }
        }
        if (enemies[0] && isPrime(floor)) //Boxten
        {
            Log("Boxten: Floor number is prime - Boxten deals 1 damage.");
            res++; 
        }
        if (enemies[1] && (enemies[21] || enemies[23]))  //Cosmo
        {
            Log("Cosmo: Dandy/Sprout is present - Cosmo deals 1 damage.");
            res++;
        }
        if (enemies[2] && (isFibonacci(floor)) && (floor > 7))  //Poppy
        {
            Log("Poppy: Floor number is present in the Fibonacci sequence and greater than 7 - Poppy deals 1 damage.");
            res++;
        }
        if (enemies[3] && inventory.Contains(10)) //Looey
        {
            Log("Looey: Player has an Air Horn in their inventory - Looey deals 1 damage. The Air Horn is used immediately.");
            res++;
        }
        if (enemies[4] && machines > 8 && characterNum != 8) //Shrimpo
        {
            Log("Shrimpo: There are more than 8 machines and the player is not Finn - Shrimpo deals 1 damage.");
            res++;
        }
        if (enemies[5] && enemies.Count(s => s) == 6) //Tisha
        {
            Log("Tisha: There are exactly 6 enemies on the floor - Tisha deals 1 damage.");
            res++;
        }
        if (enemies[6] && floor - prevBlackout == 1) //Brightney
        {
            Log("Brightney: There was a blackout on the previous floor - Brightney deals 1 damage.");
            res++;
        }
        if (enemies[7] && (enemies[8] || enemies[18])) //Connie
        {
            Log("Connie: Finn/Astro is present - Connie deals 1 damage.");
            res++;
        }
        if (enemies[8] && (machines > 8 || characterNum == 4)) //Finn
        {
            Log("Finn: There are more than 8 machines or the player is Shrimpo - Finn deals 1 damage.");
            res++;
        }
        if (enemies[9]) Log("Razzle & Dazzle: Razzle & Dazzle doesn't deal damage."); //Razzle & Dazzle
        if (enemies[10] && inventory.Contains(-1) && ichor < 160 && items.Max() > 11 && !inventory.Contains(14)) //Rodger
        {
            Log("Rodger: Player's inventory is not completely full and doesn't contain an Eject Button, player has less than 160 ichor and at least one item on the floor is Very/Ultra Rare - Rodger deals 1 damage.");
            res++;
        }
        if (enemies[11] && ichor > 600) //Teagan
        {
            Log("Teagan: Player has more than 600 ichor - Teagan deals 1 damage. 100 ichor is subtracted.");
            res++;
            ichor -= 100;
        }
        if (enemies[12] && enemies[10]) //Toodles
        {
            Log("Toodles: Rodger is present - Toodles deals 1 damage.");
            res++;
        }
        if (enemies[13]) //Flutter
        {
            //useAllID(3);
            //useAllID(12);
            if (characterNum == 7 || characterNum == 4 || (characterNum == 9 && floor % 2 == 0))
            {
                Log("Flutter: Player is Connie/Shrimpo or Razzle & Dazzle on an even floor - Flutter deals 1 damage.");
                res++;
            }
            Log($"Flutter: All Pops and Bottles o' Pop are used immediately.");
        }
    
        if (enemies[15]) //Glisten
        {
            bool glistenDamage = false;
            if (machines == 4)
            {
                Log("Glisten: There are 4 machines on the floor - Glisten deals 1 damage.");
                res++;
                glistenDamage = true;
            }
            if (characterNum == 4 || characterNum == 15)
            {
                Log($"Glisten: Player is Shrimpo/Glisten - Glisten deals 1 {(glistenDamage ? "additional " : "")}damage.");
                res++;
            }
        }
        if (enemies[17]) //Scraps
        {
            bool scrapsDamage = false;
            if (floor % 3 != 0)
            {
                Log("Scraps: Floor number is not divisible by 3 - Scraps deals 1 damage.");
                res++;
                scrapsDamage = true;
            }
            if (enemies[16])
            {
                Log($"Scraps: Goob is present - Scraps deals 1 {(scrapsDamage ? "additional " : "")}damage.");
                res++;
            }
        }
        if (enemies[18] && (machines > 7) && (machines % 4 == 0)) //Astro
        {
            Log("Astro: Floor number is divisible by 4 and greater than 7 - Astro deals 1 damage.");
            res++;
        }
        if (enemies[19] && (floor % 11 != 0 ^ enemies[23])) //Pebble
        {
            if (enemies[23])
            {
                Log("Pebble: Dandy is present and floor number is divisible by 11 - Pebble deals 1 damage.");
            }
            else
            {
                Log("Pebble: Dandy is not present and floor number is not divisible by 11 - Pebble deals 1 damage.");
            }
            res++;
        }
        if (enemies[20] && characterNum != 22 && !enemies[22] && !inventory.Contains(6) && !inventory.Contains(9)) //Shelly
        {
            Log("Shelly: Vee is not present, player is not Vee and they don't have a Skill Check Candy nor Enigma Candy in their inventory - Shelly deals 1 damage.");
            res++;
        }
        if (enemies[21] && characterNum != 2 && proteinBarsUsed % 2 == 0) //Sprout
        {
            Log("Sprout: Player is not Cosmo and the number of Protein Bars throughout the module is even - Sprout deals 1 damage.");
            res++;
        }
        if (enemies[22]) //Vee
        {
            Log("Vee: Vee deals 1 damage.");
            res++;
        }
        if (enemies[16]) //Gigi
        {
            if (inventory.Count(i => i == -1) == 3)
            {
                Log("Gigi: Player's inventory is empty - Gigi deals 1 damage.");
                res++;
            }
            else
            {
                Log($"Gigi: Player's inventory is not empty - Gigi steals the item in slot {inventory.IndexOf(i => i != -1) + 1}.");
                inventory[inventory.IndexOf(i => i != -1)] = -1;
            }
        }
        if (enemies[23] && !(enemies[18] || enemies[19] || enemies[20] || enemies[21] || enemies[22]) && DandyPoints() % 5 == 0) //Dandy
        {
            Log($"Dandy: There are no main enemies and the score obtained from the rules ({DandyPoints()}) is divisible by 5 - Dandy kills the player!");
            return 9999; 
        }
        return res;
    }

    int DandyPoints()
    {
        int points = 0;
        for (int i = 0; i < usedRarities.Count; i++) points += usedRarities[i] * (i % 2 == 0 ? -1 : 1);
        return points;
    }

    bool CanUseItem(int i)
    {
        switch (i)
        {
            case 0:
            case 6:
            case 7:
            case 9:
            case 10:
                return true;
            case 1:
            case 2:
            case 11:
            case 13:
                return enemies[6] || enemies[7] || enemies[8] || enemies[9] || enemies[10] || enemies[11] || enemies[12];
            case 3:
                return enemies[11] || enemies[12] || enemies[13] || enemies[14] || enemies[18] || enemies[19] || enemies[20] || enemies[21];
            case 4:
                return new bool[8] { enemies[11], enemies[12], enemies[13], enemies[14], enemies[18], enemies[19], enemies[20], enemies[21] }.Count(x => x) == 2;
            case 5:
                return enemies[18] || enemies[19] || enemies[20] || enemies[21] || enemies[22];
            case 14:
                return enemies[10];
            case 15:
                return enemies.Count(x => x) > 4;
            case 8:
            case 12:
            default:
                return false;
        }
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
        for (int i = 2; i < (int)Math.Sqrt(n); i++)
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

    IEnumerator StrikeStrikeStrike()
    {
        Log("You were supposed to press the submit button, but you didn't! Detonating the bomb...");
        detonating = true;
        while (enabled)
        {
            GetComponent<KMBombModule>().HandleStrike();
            yield return new WaitForSeconds(2 / 3f);
        }
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
