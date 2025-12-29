using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Text.Json;

public static class Program
{
    // ---- Модель світу ----
    sealed class Room
    {
        public string Name { get; }
        public string Description { get; }
        public Dictionary<string, string> Exits { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> Items { get; } = new();
        public List<Npc> Npcs { get; } = new();

        public Room(string name, string description)
        {
            Name = name;
            Description = description;
        }
    }

    sealed class Player
    {
        public string Name { get; set; } = "Гравець";
        public string Class { get; set; } = "Мандрівник";
        public int Hp { get; set; } = 20;
        public int MaxHp { get; set; } = 20;
        public int Power { get; set; } = 5;
    }

    sealed class Npc
    {
        public string Name { get; }
        public string Role { get; }
        public int Hp { get; set; }
        public bool Hostile { get; set; }
        public HashSet<string> LikesItems { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Npc(string name, string role, int hp, bool hostile = false)
        {
            Name = name;
            Role = role;
            Hp = hp;
            Hostile = hostile;
        }

        public string Talk()
        {
            if (Hp <= 0) return $"{Name} мовчить... (він/вона вже не може відповідати).";
            if (Hostile) return $"{Name}: \"Не підходь ближче!\"";
            return Role switch
            {
                "Продавець" => $"{Name}: \"Дивись товари. Якщо маєш яблуко — я люблю яблука.\"",
                "Брат" => $"{Name}: \"Привіт! Якщо знайдеш монету — покажи.\"",
                _ => $"{Name}: \"Привіт.\""
            };
        }
    }

    // ---- Моделі для збереження (JSON) ----
    sealed class SaveData
    {
        public int Version { get; set; } = 1;
        public string CurrentRoomId { get; set; } = "home";
        public PlayerData Player { get; set; } = new();
        public List<string> Inventory { get; set; } = new();
        public Dictionary<string, RoomData> Rooms { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    sealed class PlayerData
    {
        public string Name { get; set; } = "Гравець";
        public string Class { get; set; } = "Мандрівник";
        public int Hp { get; set; } = 20;
        public int MaxHp { get; set; } = 20;
        public int Power { get; set; } = 5;
    }

    sealed class RoomData
    {
        public List<string> Items { get; set; } = new();
        public List<NpcData> Npcs { get; set; } = new();
    }

    sealed class NpcData
    {
        public string Name { get; set; } = "";
        public int Hp { get; set; }
        public bool Hostile { get; set; }
    }

    sealed class Game
    {
    // public void RunUnitTests()
    // {
    //     Console.WriteLine("=== ЗАПУСК UNIT TESTING (Внутрішні тести) ===");

    //     // Тест 1: Перевірка ініціалізації гравця
    //     var testPlayer = new Player { Name = "Tester", Class = "воїн" };
    //     testPlayer.MaxHp = 30; testPlayer.Hp = 30;
    //     bool playerOk = (testPlayer.Hp == 30 && testPlayer.Name == "Tester");
    //     Console.WriteLine($"[Test] Player Initialization: {(playerOk ? "PASSED" : "FAILED")}");

    //     // Тест 2: Перевірка логіки діалогів NPC
    //     var testNpc = new Npc("Марко", "Продавець", 15);
    //     string speech = testNpc.Talk();
    //     bool npcOk = speech.Contains("Дивись товари");
    //     Console.WriteLine($"[Test] NPC Dialogue Logic: {(npcOk ? "PASSED" : "FAILED")}");

    //     // Тест 3: Перевірка системи кімнат (виходи)
    //     var testRoom = new Room("Тест", "Опис");
    //     testRoom.Exits["north"] = "street";
    //     bool roomOk = testRoom.Exits.ContainsKey("north") && testRoom.Exits["north"] == "street";
    //     Console.WriteLine($"[Test] Room Navigation Logic: {(roomOk ? "PASSED" : "FAILED")}");

    //     Console.WriteLine("=== UNIT TESTING ЗАВЕРШЕНО ===");
    // }
        private readonly Dictionary<string, Room> rooms = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> inventory = new();
        private readonly Player player = new();
        private string currentRoomId = "home";
        private readonly Random rng = new();

        // Файл збереження біля exe
        private static readonly string SavePath =
            Path.Combine(AppContext.BaseDirectory, "save.json");

        public Game()
        {
            BuildWorld();
        }

        private void BuildWorld()
        {
            rooms.Clear();

            var home = new Room("Дім", "Ти вдома, нічого так заспокоює як бути у своєму домі.");
            home.Items.Add("ключ");
            home.Exits["north"] = "street";
            home.Exits["n"] = "street";

            // NPC у домі
            var neighbor = new Npc("Карл", "Брат", hp: 12);
            neighbor.LikesItems.Add("монета");
            home.Npcs.Add(neighbor);

            var street = new Room("Вулиця", "Кажуть що сьогодні буде особливо холодно на вулиці.");
            street.Items.Add("монета");
            street.Exits["south"] = "home";
            street.Exits["s"] = "home";
            street.Exits["east"] = "shop";
            street.Exits["e"] = "shop";

            // Агресивний NPC на вулиці
            var goblin = new Npc("Гоблін", "Розбійник", hp: 10, hostile: true);
            street.Npcs.Add(goblin);

            var shop = new Room("Крамниця", "Маленька крамниця. Продавець мовчить. Є полиці.");
            shop.Items.Add("яблуко");
            shop.Exits["west"] = "street";
            shop.Exits["w"] = "street";

            var seller = new Npc("Марко", "Продавець", hp: 15);
            seller.LikesItems.Add("яблуко");
            shop.Npcs.Add(seller);

            rooms["home"] = home;
            rooms["street"] = street;
            rooms["shop"] = shop;
        }

        public void Run()
        {
            Console.OutputEncoding = Encoding.UTF8;

            PrintWelcome();

            // Якщо є збереження — підкажемо
            if (File.Exists(SavePath))
                Console.WriteLine($"Знайдено save.json. Можеш написати: load");

            CreateCharacter();
            Look();

            while (true)
            {
                if (player.Hp <= 0)
                {
                    Console.WriteLine("\nТи програв. Персонаж без сил.");
                    break;
                }

                Console.Write("\n> ");
                var line = Console.ReadLine();
                if (line == null) break;

                var input = line.Trim();
                if (input.Length == 0) continue;

                if (!HandleCommand(input))
                    break;
            }
        }

        private void PrintWelcome()
        {
            Console.WriteLine("Команди: look, go <dir>, take <item>, drop <item>, inv, stats, talk <npc>, give <item> <npc>, attack <npc>, save, load, help, exit");
        }

        private void CreateCharacter()
        {
            Console.WriteLine("\nСтворення персонажа:");
            Console.Write("Ім'я: ");
            var name = (Console.ReadLine() ?? "").Trim();
            if (!string.IsNullOrWhiteSpace(name)) player.Name = name;

            Console.Write("Клас (воїн/розвідник/маг) або будь-який текст: ");
            var cls = (Console.ReadLine() ?? "").Trim();
            if (!string.IsNullOrWhiteSpace(cls)) player.Class = cls;

            // Дуже проста “статистика” по класу
            var c = player.Class.Trim().ToLowerInvariant();
            if (c.Contains("воїн"))
            {
                player.MaxHp = 30; player.Hp = 30; player.Power = 7;
            }
            else if (c.Contains("розвідник"))
            {
                player.MaxHp = 22; player.Hp = 22; player.Power = 6;
            }
            else if (c.Contains("маг"))
            {
                player.MaxHp = 18; player.Hp = 18; player.Power = 8;
            }

            Console.WriteLine($"Готово: {player.Name} ({player.Class}), HP {player.Hp}/{player.MaxHp}, Power {player.Power}");
        }

        private bool HandleCommand(string input)
        {
            var parts = SplitArgs(input);
            var cmd = parts[0].ToLowerInvariant();
            var arg = parts.Count > 1 ? string.Join(' ', parts.Skip(1)) : "";

            switch (cmd)
            {
                case "help":
                case "?":
                    Help();
                    return true;

                case "look":
                case "l":
                    Look();
                    return true;

                case "go":
                case "move":
                    if (string.IsNullOrWhiteSpace(arg))
                        Console.WriteLine("Куди? Приклад: go north");
                    else
                        Go(arg);
                    return true;

                // Швидкі напрямки як команди:
                case "n":
                case "north":
                case "s":
                case "south":
                case "e":
                case "east":
                case "w":
                case "west":
                    Go(cmd);
                    return true;

                case "take":
                case "get":
                    if (string.IsNullOrWhiteSpace(arg))
                        Console.WriteLine("Що взяти? Приклад: take ключ");
                    else
                        Take(arg);
                    return true;

                case "drop":
                    if (string.IsNullOrWhiteSpace(arg))
                        Console.WriteLine("Що викинути? Приклад: drop монета");
                    else
                        Drop(arg);
                    return true;

                case "inv":
                case "inventory":
                case "i":
                    ShowInventory();
                    return true;

                case "stats":
                    ShowStats();
                    return true;

                case "npcs":
                    ShowNpcs();
                    return true;

                case "talk":
                    if (string.IsNullOrWhiteSpace(arg))
                        Console.WriteLine("З ким говорити? Приклад: talk Марко");
                    else
                        Talk(arg);
                    return true;

                case "give":
                    if (parts.Count < 3)
                    {
                        Console.WriteLine("Формат: give <предмет> <npc>. Приклад: give яблуко Марко");
                        return true;
                    }
                    Give(parts[1], string.Join(' ', parts.Skip(2)));
                    return true;

                case "attack":
                    if (string.IsNullOrWhiteSpace(arg))
                        Console.WriteLine("Кого атакувати? Приклад: attack Гопник");
                    else
                        Attack(arg);
                    return true;

                // ---- збереження/завантаження ----
                case "save":
                    SaveGame();
                    return true;

                case "load":
                    LoadGame();
                    return true;

                case "exit":
                case "quit":
                    Console.WriteLine("Вихід з гри.");
                    return false;

                default:
                    Console.WriteLine($"Невідома команда: {cmd}. Напиши help.");
                    return true;
            }
        }

        private void Help()
        {
            Console.WriteLine("Команди:");
            Console.WriteLine("  look / l                     — оглянути локацію");
            Console.WriteLine("  go <dir>                      — піти (north/south/east/west або n/s/e/w)");
            Console.WriteLine("  take <item>                   — взяти предмет");
            Console.WriteLine("  drop <item>                   — викинути предмет");
            Console.WriteLine("  inv / i                       — інвентар");
            Console.WriteLine("  stats                         — характеристики персонажа");
            Console.WriteLine("  npcs                          — список NPC у локації");
            Console.WriteLine("  talk <npc>                    — поговорити з NPC");
            Console.WriteLine("  give <item> <npc>             — дати предмет NPC");
            Console.WriteLine("  attack <npc>                  — атакувати NPC");
            Console.WriteLine("  save                          — зберегти гру (save.json)");
            Console.WriteLine("  load                          — завантажити гру (save.json)");
            Console.WriteLine("  help / ?                      — довідка");
            Console.WriteLine("  exit                          — вихід");
        }

        private void Look()
        {
            var room = rooms[currentRoomId];
            Console.WriteLine($"\n[{room.Name}]");
            Console.WriteLine(room.Description);

            var exits = room.Exits.Keys
                .Select(NormalizeDir)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x);
            Console.WriteLine("Виходи: " + (exits.Any() ? string.Join(", ", exits) : "немає"));

            if (room.Items.Count > 0)
                Console.WriteLine("Предмети: " + string.Join(", ", room.Items));

            var aliveNpcs = room.Npcs.Where(n => n.Hp > 0).ToList();
            if (aliveNpcs.Count > 0)
                Console.WriteLine("Персонажі: " + string.Join(", ", aliveNpcs.Select(n => $"{n.Name} ({n.Role})")));
        }

        private void Go(string dir)
        {
            var room = rooms[currentRoomId];

            if (!room.Exits.TryGetValue(dir, out var nextId))
            {
                Console.WriteLine("Туди не пройти.");
                return;
            }

            currentRoomId = nextId;
            Look();
        }

        private void Take(string item)
        {
            var room = rooms[currentRoomId];
            var found = room.Items.FirstOrDefault(x => x.Equals(item, StringComparison.OrdinalIgnoreCase));

            if (found == null)
            {
                Console.WriteLine("Тут такого немає.");
                return;
            }

            room.Items.Remove(found);
            inventory.Add(found);
            Console.WriteLine($"Ти взяв: {found}");
        }

        private void Drop(string item)
        {
            var found = inventory.FirstOrDefault(x => x.Equals(item, StringComparison.OrdinalIgnoreCase));
            if (found == null)
            {
                Console.WriteLine("У тебе цього немає.");
                return;
            }

            inventory.Remove(found);
            rooms[currentRoomId].Items.Add(found);
            Console.WriteLine($"Ти викинув: {found}");
        }

        private void ShowInventory()
        {
            if (inventory.Count == 0)
                Console.WriteLine("Інвентар порожній.");
            else
                Console.WriteLine("Інвентар: " + string.Join(", ", inventory));
        }

        private void ShowStats()
        {
            Console.WriteLine($"{player.Name} ({player.Class})");
            Console.WriteLine($"HP: {player.Hp}/{player.MaxHp}");
            Console.WriteLine($"Power: {player.Power}");
        }

        private void ShowNpcs()
        {
            var room = rooms[currentRoomId];
            var alive = room.Npcs.Where(n => n.Hp > 0).ToList();
            if (alive.Count == 0)
            {
                Console.WriteLine("Тут немає персонажів.");
                return;
            }

            foreach (var n in alive)
                Console.WriteLine($"- {n.Name} ({n.Role}) HP:{n.Hp}" + (n.Hostile ? " [ворожий]" : ""));
        }

        private Npc? FindNpcHere(string name)
        {
            var room = rooms[currentRoomId];
            return room.Npcs.FirstOrDefault(n => n.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && n.Hp > 0);
        }

        private void Talk(string npcName)
        {
            var npc = FindNpcHere(npcName);
            if (npc == null)
            {
                Console.WriteLine("Тут немає такого персонажа.");
                return;
            }

            Console.WriteLine(npc.Talk());
        }

        private void Give(string item, string npcName)
        {
            var npc = FindNpcHere(npcName);
            if (npc == null)
            {
                Console.WriteLine("Тут немає такого персонажа.");
                return;
            }

            var invItem = inventory.FirstOrDefault(x => x.Equals(item, StringComparison.OrdinalIgnoreCase));
            if (invItem == null)
            {
                Console.WriteLine("У тебе немає цього предмета.");
                return;
            }

            inventory.Remove(invItem);

            if (npc.LikesItems.Contains(invItem))
            {
                npc.Hostile = false;
                Console.WriteLine($"{npc.Name}: \"О, дякую! Тепер я до тебе добре ставлюсь.\"");

                // маленька нагорода
                if (rng.Next(0, 2) == 0)
                {
                    player.Hp = Math.Min(player.MaxHp, player.Hp + 3);
                    Console.WriteLine("Ти почуваєшся краще (+3 HP).");
                }
                else
                {
                    inventory.Add("ліхтарик");
                    Console.WriteLine("Тобі дали подарунок: ліхтарик.");
                }
            }
            else
            {
                Console.WriteLine($"{npc.Name}: \"Ем... навіщо мені це?\" (предмет загубився десь у світі)");
            }
        }

        private void Attack(string npcName)
        {
            var npc = FindNpcHere(npcName);
            if (npc == null)
            {
                Console.WriteLine("Тут немає такого персонажа.");
                return;
            }

            npc.Hostile = true;

            // Гравець б'є
            int dmg = rng.Next(player.Power - 1, player.Power + 2);
            if (dmg < 1) dmg = 1;
            npc.Hp -= dmg;

            Console.WriteLine($"Ти вдарив {npc.Name} на {dmg}.");
            if (npc.Hp <= 0)
            {
                Console.WriteLine($"{npc.Name} переможений.");
                return;
            }

            // NPC відповідає
            int retaliate = rng.Next(2, 6);
            player.Hp -= retaliate;
            Console.WriteLine($"{npc.Name} відповів атакою на {retaliate}. Твоє HP: {player.Hp}/{player.MaxHp}");
        }

        // ---- ЗБЕРЕЖЕННЯ / ЗАВАНТАЖЕННЯ ----
        private void SaveGame()
        {
            try
            {
                var save = new SaveData
                {
                    CurrentRoomId = currentRoomId,
                    Player = new PlayerData
                    {
                        Name = player.Name,
                        Class = player.Class,
                        Hp = player.Hp,
                        MaxHp = player.MaxHp,
                        Power = player.Power
                    },
                    Inventory = inventory.ToList()
                };

                foreach (var kv in rooms)
                {
                    var roomId = kv.Key;
                    var room = kv.Value;

                    var rd = new RoomData
                    {
                        Items = room.Items.ToList(),
                        Npcs = room.Npcs.Select(n => new NpcData
                        {
                            Name = n.Name,
                            Hp = n.Hp,
                            Hostile = n.Hostile
                        }).ToList()
                    };

                    save.Rooms[roomId] = rd;
                }

                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(save, options);
                File.WriteAllText(SavePath, json, Encoding.UTF8);

                Console.WriteLine($"Збережено: {SavePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Помилка збереження: {ex.Message}");
            }
        }

        private void LoadGame()
        {
            try
            {
                if (!File.Exists(SavePath))
                {
                    Console.WriteLine("Немає файлу збереження (save.json).");
                    return;
                }

                var json = File.ReadAllText(SavePath, Encoding.UTF8);
                var save = JsonSerializer.Deserialize<SaveData>(json);

                if (save == null)
                {
                    Console.WriteLine("Не вдалося прочитати save.json.");
                    return;
                }

                BuildWorld();

                player.Name = save.Player.Name;
                player.Class = save.Player.Class;
                player.Hp = save.Player.Hp;
                player.MaxHp = save.Player.MaxHp;
                player.Power = save.Player.Power;

                inventory.Clear();
                inventory.AddRange(save.Inventory ?? new List<string>());

                if (save.Rooms != null)
                {
                    foreach (var kv in save.Rooms)
                    {
                        if (!rooms.TryGetValue(kv.Key, out var room)) continue;

                        room.Items.Clear();
                        room.Items.AddRange(kv.Value.Items ?? new List<string>());

                        foreach (var npcState in kv.Value.Npcs ?? new List<NpcData>())
                        {
                            var npc = room.Npcs.FirstOrDefault(n => n.Name.Equals(npcState.Name, StringComparison.OrdinalIgnoreCase));
                            if (npc != null)
                            {
                                npc.Hp = npcState.Hp;
                                npc.Hostile = npcState.Hostile;
                            }
                        }
                    }
                }

                if (!string.IsNullOrWhiteSpace(save.CurrentRoomId) && rooms.ContainsKey(save.CurrentRoomId))
                    currentRoomId = save.CurrentRoomId;
                else
                    currentRoomId = "home";

                Console.WriteLine("Завантаження успішне.");
                Look();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Помилка завантаження: {ex.Message}");
            }
        }

        private static List<string> SplitArgs(string input)
        {
            var result = new List<string>();
            var cur = "";
            bool inQuotes = false;

            foreach (var ch in input)
            {
                if (ch == '"')
                {
                    inQuotes = !inQuotes;
                    continue;
                }

                if (!inQuotes && char.IsWhiteSpace(ch))
                {
                    if (cur.Length > 0) { result.Add(cur); cur = ""; }
                    continue;
                }

                cur += ch;
            }
            if (cur.Length > 0) result.Add(cur);

            return result.Count == 0 ? new List<string> { "" } : result;
        }

        private static string NormalizeDir(string d) => d.ToLowerInvariant() switch
        {
            "n" => "north",
            "s" => "south",
            "e" => "east",
            "w" => "west",
            _ => d
        };
    }

    public static void Main()
    {
        new Game().Run();
        //     Console.OutputEncoding = System.Text.Encoding.UTF8;
    
        //     var game = new Game();
        //     game.RunUnitTests();

        //     Console.WriteLine("\nТестування завершено. Натисніть будь-яку клавішу, щоб вийти...");
        //     Console.ReadKey();
    }
}
