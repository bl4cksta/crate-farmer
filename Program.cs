using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace CrateFarmer
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Welcome to Crate Farmer!");

            // startups
            var loadedCrates = CrateManager.LoadCrates();
            if (loadedCrates > 0) 
                Debug.Log($"Total crates loaded: {loadedCrates}");
            else  // если ни один файл не подошёл
            {
                Debug.Log("Error loading crates!");
                Console.WriteLine("Press any key to exit");
                Console.ReadKey();
                return;
            }

            Inventory inventory = new Inventory();
            inventory.LoadInventory();

            // gameloop
            while (true)
            {
                Console.WriteLine("Choose crate: 1 - 3");

                int n;
                // если вводим не то что нам нужно
                if (!int.TryParse(Console.ReadLine(), out n) || n <= 0 || n > 3)
                {
                    RepeatEnter();
                    inventory.Show(0);
                    Console.SetCursorPosition(0, 1);
                    continue;
                }
                // main gameplay
                Console.Clear();
                var itemsToAdd = CrateManager.OpenCrate(n);
                var itemsAdded = inventory.AddItems(itemsToAdd);
                inventory.Show(itemsAdded);
                Console.SetCursorPosition(0, itemsAdded + 3);

                WaitForRepeat();
                
                Console.Clear();
                inventory.Show(-1);
                Console.SetCursorPosition(0, 0);
            }
        }
        static void RepeatEnter()
        {
            Console.Clear();
            Console.SetCursorPosition(0, 0);
            Console.WriteLine("> Enter only 1, 2 or 3! <"); // hint
        }
        static void WaitForRepeat()
        {
            Console.WriteLine("Press any key to repeat");
            Console.ReadKey();
        }
    }
    static class Settings
    {
        public const bool isDebugOn = false;
        public const int itemTypesCount = 5;
        public enum ItemType
        {
            HealPotion,
            ManaPotion,
            Coin,
            Sword,
            Ring
        }
    }
    static class Debug
    {
        public static void Log(string str)
        {
            if (Settings.isDebugOn) Console.WriteLine(str);
        }
    }
    class CrateManager
    {
        private static List<Crate> crates;
        private static int fullDropChance;
        public static int LoadCrates()
        {
            if (crates != null) return 0;

            var dirPath = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "Crates");

            // создаём папку с тестовым сундуком если её нет
            if (!Directory.Exists(dirPath)) CreateTestCrate(dirPath);

            // считываем сундуки
            crates = new List<Crate>();
            fullDropChance = 0;
            var cratesToLoad = Directory.GetFiles(dirPath);

            // создаём тестовый сундук если есть папка но она пустая
            if (cratesToLoad.Length == 0)
            {
                cratesToLoad = new string[1];
                cratesToLoad[0] = CreateTestCrate(dirPath);
            }
            var loadedCrates = 0;
            foreach (var file in cratesToLoad)
            {
                try
                {
                    // считываем файл
                    var lines = File.ReadAllLines(file);

                    // задаём шанс выпадения именно этого сундука
                    int dropChance = JsonSerializer.Deserialize<Drop>(lines[0]).Dropchance;

                    // задаём список вещей которые могут выпасть
                    List<Item> items = new List<Item>(); // optimize arrays?
                    for (int i = 1; i < lines.Length; i++) items.Add(JsonSerializer.Deserialize<Item>(lines[i]));

                    // добавляем в список
                    var crate = new Crate(dropChance, items, Path.GetFileNameWithoutExtension(file));
                    crates.Add(crate);
                    loadedCrates++;
                    fullDropChance += dropChance;
                    Debug.Log($"Crate loaded: {Path.GetFileName(file)}");
                }
                catch { Debug.Log($"Error loading {Path.GetFileName(file)}"); }
            }
            return loadedCrates;
        }
        // выбираем случайный сундук
        public static List<Item> OpenCrate(int crateEntered)
        {
            var random = new Random();
            // простой рандом из общего списка сундуков
            //var crateNumber = random.Next(crates.Count); 
            //Console.WriteLine($"Opened crate #{crateEntered}"); // ИЛИ crateNumber => номер из папки /Crates/
            //return crates[crateNumber].Items;

            // рандом с настраиваемым шансом выпадения
            var pickedChance = random.Next(fullDropChance);
            var counter = 0;
            foreach (var crate in crates)
            {
                counter += crate.Dropchance;
                if (counter >= pickedChance)
                {
                    Console.WriteLine($"Opened crate \"{crate.Name}\""); // ИЛИ crateEntered => номер который мы ввели
                    return crate.Items;
                }
            }
            return null;
        }
        // создаём тестовый сундук
        private static string CreateTestCrate(string directory)
        {
            Directory.CreateDirectory(directory);

            string[] file = new string[Settings.itemTypesCount+1];
            file[0] = JsonSerializer.Serialize(new Drop(100));
            for (int i = 0; i < Settings.itemTypesCount; i++)
            {
                Item test = new Item((Settings.ItemType)i, 1, 100 - 15 * i);
                file[i+1] = JsonSerializer.Serialize(test);
            }
            var filePath = Path.Combine(directory, "TestCrate.json");
            File.WriteAllLines(filePath, file);
            return filePath;
        }
    }
    class Drop
    {
        public int Dropchance { get; set; }
        public Drop(int dropchance)
        {
            Dropchance = dropchance;
        }
    }
    class Crate : Drop
    {
        public List<Item> Items { get; set; }
        public string Name { get; set; }
        public Crate(int chance, List<Item> items, string name) : base(chance)
        {
            Items = items;
            Name = name;
        }
    }
    class Item : Drop
    {
        public Settings.ItemType Type { get; set; }
        public int Count { get; set; }
        public Item(Settings.ItemType type, int count, int dropchance) : base(dropchance)
        {
            Type = type;
            Count = count;
        }
    }
    class Inventory
    {
        private int[] items; // простой вариант, храним только количество
        //public static List<Item> items; // сложный вариант, если у вещей будут какие-то отдельные параметры (урон меча, хп на отхил и тд)
        internal void LoadInventory()
        {
            items = new int[Settings.itemTypesCount];
            //items = new List<Item>();

            for (int i = 0; i < items.Length; i++)
            {
                // можно сделать загрузку инвентаря
                items[i] = 0;
            }
        }
        internal int AddItems(List<Item> itemsToAdd)
        {
            var lineCounter = 0;
            var addedItems = new int[Settings.itemTypesCount];

            // просчитываем шанс выпадения отдельных предметов
            foreach (var i in itemsToAdd)
            {
                var random = new Random();
                if (random.Next(100) < i.Dropchance) addedItems[(int)i.Type] += i.Count;

            }

            // добавляем предметы в инвентарь и выводим на экран общее кол-во
            for (Settings.ItemType type = 0; (int)type < Settings.itemTypesCount; type++)
            {
                var count = addedItems[(int)type];
                if (count == 0) continue;
                items[(int)type] += count;
                lineCounter++;
                Console.WriteLine($"Added: {type} - {count} pcs");
            }

            // если ничего не выпало
            if (lineCounter == 0)
            {
                Console.WriteLine("Crate is empty!");
                lineCounter = 1;
            }

            return lineCounter;
        }
        internal void Show(int skippedLines)
        {
            // пропускаем несколько линий чтоб было красиво
            for (int i = 0; i < Settings.itemTypesCount + 6 - skippedLines; i++) Console.WriteLine();

            Console.WriteLine(" > > > INVENTORY < < < ");

            // выводим общее количество вещей в инвентаре
            for (Settings.ItemType type = 0; (int)type < Settings.itemTypesCount; type++)
            {
                var count = items[(int)type];
                if (count == 0) continue;
                Console.WriteLine($"{type} : {count}");
            }
        }
    }

}
