using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace OldenEraTemplateEditor.Services.ContentManagement
{
    public class SidMapping
    {
        public string Sid { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public static class ContentIds
    {
        public static IReadOnlyList<SidMapping> GetAll() => SidReflection.GetSidMappings(typeof(ContentIds));
        public static readonly SidMapping AlchemyLab = new() { Sid = "alchemy_lab", Name = "Алхимическая лаборатория" };
        public static readonly SidMapping Arena = new() { Sid = "arena", Name = "Арена" };
        public static readonly SidMapping BeerFountain = new() { Sid = "beer_fountain", Name = "Пивной фонтан" };
        public static readonly SidMapping BorealCall = new() { Sid = "boreal_call", Name = "Зов севера" };
        public static readonly SidMapping CelestialSphere = new() { Sid = "celestial_sphere", Name = "Небесная сфера" };
        public static readonly SidMapping AltarOfMagic1 = new() { Sid = "altar_of_magic_1", Name = "Святилище Ночной тени" };
        public static readonly SidMapping AltarOfMagic2 = new() { Sid = "altar_of_magic_2", Name = "Святилище Дневного света" };
        public static readonly SidMapping AltarOfMagic3 = new() { Sid = "altar_of_magic_3", Name = "Тайное святилище" };
        public static readonly SidMapping AltarOfMagic4 = new() { Sid = "altar_of_magic_4", Name = "Первозданное святилище" };
        public static readonly SidMapping MagicAmplifier1 = new() { Sid = "magic_amplifier_1", Name = "Усилитель Ночной тени" };
        public static readonly SidMapping MagicAmplifier2 = new() { Sid = "magic_amplifier_2", Name = "Усилитель Дневного света" };
        public static readonly SidMapping MagicAmplifier3 = new() { Sid = "magic_amplifier_3", Name = "Тайный усилитель" };
        public static readonly SidMapping MagicAmplifier4 = new() { Sid = "magic_amplifier_4", Name = "Первозданный усилитель" };
        public static readonly SidMapping Chimerologist = new() { Sid = "chimerologist", Name = "Химеролог" };
        public static readonly SidMapping Circus = new() { Sid = "circus", Name = "Цирк" };
        public static readonly SidMapping CollegeOfWonder = new() { Sid = "college_of_wonder", Name = "Колледж чудес" };
        public static readonly SidMapping CrystalTrail = new() { Sid = "crystal_trail", Name = "Кристальная тропа" };
        public static readonly SidMapping DragonUtopia = new() { Sid = "dragon_utopia", Name = "Утопия драконов" };
        public static readonly SidMapping EternalDragon = new() { Sid = "eternal_dragon", Name = "Вечный дракон" };
        public static readonly SidMapping FickleShrine = new() { Sid = "fickle_shrine", Name = "Капризное святилище" };
        public static readonly SidMapping FlatteringMirror = new() { Sid = "flattering_mirror", Name = "Льстивое зеркало" };
        public static readonly SidMapping Forge = new() { Sid = "forge", Name = "Кузница" };
        public static readonly SidMapping Fort = new() { Sid = "fort", Name = "Форт" };
        public static readonly SidMapping Fountain = new() { Sid = "fountain", Name = "Фонтан" };
        public static readonly SidMapping Fountain2 = new() { Sid = "fountain_2", Name = "Фонтан 2" };
        public static readonly SidMapping HuntsmansCamp = new() { Sid = "huntsmans_camp", Name = "Лагерь охотников" };
        public static readonly SidMapping InfernalCirque = new() { Sid = "infernal_cirque", Name = "Инфернальный цирк" };
        public static readonly SidMapping InsarasEye = new() { Sid = "insaras_eye", Name = "Око Инсары" };
        public static readonly SidMapping JoustingRange = new() { Sid = "jousting_range", Name = "Турнирное поле" };
        public static readonly SidMapping ManaWell = new() { Sid = "mana_well", Name = "Колодец маны" };
        public static readonly SidMapping Market = new() { Sid = "market", Name = "Рынок" };
        public static readonly SidMapping MineCrystals = new() { Sid = "mine_crystals", Name = "Кристальная жила" };
        public static readonly SidMapping MineGemstones = new() { Sid = "mine_gemstones", Name = "Самоцветный холм" };
        public static readonly SidMapping MineGold = new() { Sid = "mine_gold", Name = "Золотая шахта" };
        public static readonly SidMapping MineMercury = new() { Sid = "mine_mercury", Name = "Ртутный разлом" };
        public static readonly SidMapping MineOre = new() { Sid = "mine_ore", Name = "Рудник" };
        public static readonly SidMapping MineWood = new() { Sid = "mine_wood", Name = "Лесопилка" };
        public static readonly SidMapping Mirage = new() { Sid = "mirage", Name = "Мираж" };
        public static readonly SidMapping MysteriousStone = new() { Sid = "mysterious_stone", Name = "Таинственный камень" };
        public static readonly SidMapping MysticalTower = new() { Sid = "mystical_tower", Name = "Мистическая башня" };
        public static readonly SidMapping ScrollBox = new() { Sid = "scroll_box", Name = "Магический свиток" };
        public static readonly SidMapping EnchantedScrollBox = new() { Sid = "enchanted_scroll_box", Name = "Зачарованный свиток" };
        public static readonly SidMapping MythicScrollBox = new() { Sid = "mythic_scroll_box", Name = "Мифический свиток" };
        public static readonly SidMapping OrbObservatory = new() { Sid = "orb_observatory", Name = "Обсерватория сфер" };
        public static readonly SidMapping PandoraBox = new() { Sid = "pandora_box", Name = "Ящик Пандоры" };
        public static readonly SidMapping PetrifiedMemorial = new() { Sid = "petrified_memorial", Name = "Окаменевший мемориал" };
        public static readonly SidMapping PileOfBooks = new() { Sid = "pile_of_books", Name = "Груда книг" };
        public static readonly SidMapping PointOfBalance = new() { Sid = "point_of_balance", Name = "Точка равновесия" };
        public static readonly SidMapping Prison = new() { Sid = "prison", Name = "Тюрьма" };
        public static readonly SidMapping QuixsPath = new() { Sid = "quixs_path", Name = "Путь Квикса" };
        public static readonly SidMapping RandomHire1 = new() { Sid = "random_hire_1", Name = "Случайный найм ур. 1" };
        public static readonly SidMapping RandomHire2 = new() { Sid = "random_hire_2", Name = "Случайный найм ур. 2" };
        public static readonly SidMapping RandomHire3 = new() { Sid = "random_hire_3", Name = "Случайный найм ур. 3" };
        public static readonly SidMapping RandomHire4 = new() { Sid = "random_hire_4", Name = "Случайный найм ур. 4" };
        public static readonly SidMapping RandomHire5 = new() { Sid = "random_hire_5", Name = "Случайный найм ур. 5" };
        public static readonly SidMapping RandomHire6 = new() { Sid = "random_hire_6", Name = "Случайный найм ур. 6" };
        public static readonly SidMapping RandomHire7 = new() { Sid = "random_hire_7", Name = "Случайный найм ур. 7" };
        public static readonly SidMapping RandomItemCommon = new() { Sid = "random_item_common", Name = "Случайный предмет (обычный)" };
        public static readonly SidMapping RandomItemEpic = new() { Sid = "random_item_epic", Name = "Случайный предмет (эпический)" };
        public static readonly SidMapping RandomItemLegendary = new() { Sid = "random_item_legendary", Name = "Случайный предмет (легендарный)" };
        public static readonly SidMapping RandomItemRare = new() { Sid = "random_item_rare", Name = "Случайный предмет (редкий)" };
        public static readonly SidMapping RemoteFoothold = new() { Sid = "remote_foothold", Name = "Удалённый плацдарм" };
        public static readonly SidMapping ResearchLaboratory = new() { Sid = "research_laboratory", Name = "Исследовательская лаборатория" };
        public static readonly SidMapping RitualPyre = new() { Sid = "ritual_pyre", Name = "Ритуальный костёр" };
        public static readonly SidMapping SacrificialShrine = new() { Sid = "sacrificial_shrine", Name = "Жертвенное святилище" };
        public static readonly SidMapping ShadyDen = new() { Sid = "shady_den", Name = "Тёмное логово" };
        public static readonly SidMapping Stables = new() { Sid = "stables", Name = "Конюшни" };
        public static readonly SidMapping Tavern = new() { Sid = "tavern", Name = "Таверна" };
        public static readonly SidMapping TearOfTruth = new() { Sid = "tear_of_truth", Name = "Слеза истины" };
        public static readonly SidMapping TheGorge = new() { Sid = "the_gorge", Name = "Груда падали" }; // Mismatch from SID, but that's the name shown in-game.
        public static readonly SidMapping TownGate = new() { Sid = "town_gate", Name = "Городские врата" };
        public static readonly SidMapping TreeOfAbundance = new() { Sid = "tree_of_abundance", Name = "Древо изобилия" };
        public static readonly SidMapping TroglodyteThrone = new() { Sid = "troglodyte_throne", Name = "Трон троглодитов" };
        public static readonly SidMapping TwilightBloom = new() { Sid = "twilight_bloom", Name = "Сумеречный цвет" };
        public static readonly SidMapping UnforgottenGrave = new() { Sid = "unforgotten_grave", Name = "Незабытая могила" };
        public static readonly SidMapping University = new() { Sid = "university", Name = "Университет" };
        public static readonly SidMapping UnstableRuins = new() { Sid = "unstable_ruins", Name = "Нестабильные руины" };
        public static readonly SidMapping Watchtower = new() { Sid = "watchtower", Name = "Сторожевая башня" };
        public static readonly SidMapping WindRose = new() { Sid = "wind_rose", Name = "Роза ветров" };
        public static readonly SidMapping WiseOwl = new() { Sid = "wise_owl", Name = "Мудрая сова" };
        public static readonly SidMapping StorageWood = new() { Sid = "storage_wood", Name = "Склад древесины" };
        public static readonly SidMapping StorageOre = new() { Sid = "storage_ore", Name = "Склад руды" };
        public static readonly SidMapping StorageGold = new() { Sid = "storage_gold", Name = "Склад золота" };
        public static readonly SidMapping StorageMercury = new() { Sid = "storage_mercury", Name = "Склад ртути" };
        public static readonly SidMapping StorageCrystals = new() { Sid = "storage_crystals", Name = "Склад кристаллов" };
        public static readonly SidMapping StorageGemstones = new() { Sid = "storage_gemstones", Name = "Склад самоцветов" };
        public static readonly SidMapping StorageDust = new() { Sid = "storage_dust", Name = "Склад пыли" };
        public static readonly SidMapping Gardener = new() { Sid = "gardener", Name = "Садовник" };
        public static readonly SidMapping Windmill = new() { Sid = "windmill", Name = "Мельница" };
        public static readonly SidMapping Village = new() { Sid = "village", Name = "Деревня" };
        public static readonly SidMapping GingerbreadHouse = new() { Sid = "gingerbread_house", Name = "Пряничный домик" };
        public static readonly SidMapping PeasantCart = new() { Sid = "peasant_cart", Name = "Крестьянская повозка" };
        public static readonly SidMapping AbandonedCorpse = new() { Sid = "abandoned_corpse", Name = "Брошенный труп" };
        public static readonly SidMapping AbandonedMansion = new() { Sid = "abandoned_mansion", Name = "Заброшенный особняк" };
        public static readonly SidMapping AbnormalStructure = new() { Sid = "abnormal_structure", Name = "Аномальная постройка" };
        public static readonly SidMapping AlvarsEye = new() { Sid = "alvars_eye", Name = "Око Альвара" };
        public static readonly SidMapping BlackTower = new() { Sid = "black_tower", Name = "Чёрная башня" };
        public static readonly SidMapping CircleOfLife = new() { Sid = "circle_of_life", Name = "Круг жизни" };
        public static readonly SidMapping CursedOldHouse = new() { Sid = "cursed_old_house", Name = "Проклятый старый дом" };
        public static readonly SidMapping CrowNest = new() { Sid = "crow_nest", Name = "Воронье гнездо" };
        public static readonly SidMapping GoblinCache = new() { Sid = "goblin_cache", Name = "Тайник гоблинов" };
        public static readonly SidMapping IridescentAbbey = new() { Sid = "iridescent_abbey", Name = "Переливчатое аббатство" };
        public static readonly SidMapping LegionsMemorial = new() { Sid = "legions_memorial", Name = "Мемориал легионов" };
        public static readonly SidMapping MereasShrine = new() { Sid = "mereas_shrine", Name = "Святилище Мереи" };
        public static readonly SidMapping MontyHall = new() { Sid = "monty_hall", Name = "Зал Монти" };
        public static readonly SidMapping OvergrownGrave = new() { Sid = "overgrown_grave", Name = "Заросшая могила" };
        public static readonly SidMapping PrismaticLair = new() { Sid = "prismatic_lair", Name = "Призматическое логово" };
        public static readonly SidMapping RaidersCamp = new() { Sid = "raiders_camp", Name = "Лагерь разбойников" };
        public static readonly SidMapping HerosCrypt = new() { Sid = "heros_crypt", Name = "Склеп героя" };
        public static readonly SidMapping UncannyRite = new() { Sid = "uncanny_rite", Name = "Жуткий обряд" };
        public static readonly SidMapping LearningStone = new() { Sid = "learning_stone", Name = "Камень знаний" };
        public static readonly SidMapping LostLibrary = new() { Sid = "lost_library", Name = "Потерянная библиотека" };
        public static readonly SidMapping TreeOfKnowledge = new() { Sid = "tree_of_knowledge", Name = "Древо познания" };
        public static readonly SidMapping StingingSword = new() { Sid = "stinging_sword", Name = "Жалящий меч" };
        public static readonly SidMapping ArmoryAutomaton = new() { Sid = "armory_automaton", Name = "Оружейный автоматон" };
        public static readonly SidMapping MagicWheel = new() { Sid = "magic_wheel", Name = "Магическое колесо" };
        public static readonly SidMapping KnowledgeGarden = new() { Sid = "knowledge_garden", Name = "Сад знаний" };
        public static readonly SidMapping Maze = new() { Sid = "maze", Name = "Лабиринт" };
        public static readonly SidMapping TrialScales = new() { Sid = "trial_scales", Name = "Весы испытания" };
        public static readonly SidMapping MercenaryGuild = new() { Sid = "mercenary_guild", Name = "Гильдия наёмников" };
        
    }

    public static class IncludeListIds
    {
        /* string identifier for include lists, to properly handle content item creation. (These are not real SID values of content items, but names of their include lists) */
        public static readonly string Identifier = "content";
        public static IReadOnlyList<SidMapping> GetAll() => SidReflection.GetSidMappings(typeof(IncludeListIds));

        public static readonly SidMapping RandomHiresLowTier = new() { Sid = "content_list_building_random_hires_low_tier", Name = "Случайный найм (низкий ур.)" };
        public static readonly SidMapping RandomHiresHighTier = new() { Sid = "content_list_building_random_hires_high_tier", Name = "Случайный найм (высокий ур.)" };
        public static readonly SidMapping RandomHiresAllTier = new() { Sid = "basic_content_list_building_random_hires", Name = "Случайный найм (любой ур.)" };
        public static readonly SidMapping RandomHiresAllTierWeighted = new() { Sid = "content_list_building_random_hires", Name = "Случайный найм (любой ур., взвеш.)" };
        public static readonly SidMapping ResourceBanksTier1 = new() { Sid = "basic_content_list_building_guarded_resource_banks_tier_1", Name = "Хранилища ресурсов Т1" };
        public static readonly SidMapping ResourceBanksTier2 = new() { Sid = "basic_content_list_building_guarded_resource_banks_tier_2", Name = "Хранилища ресурсов Т2" };
        public static readonly SidMapping GuardedBanksTier1 = new() { Sid = "basic_content_list_building_guarded_resource_banks_tier_1", Name = "Охраняемые банки Т1" };
        public static readonly SidMapping GuardedBanksTier2 = new() { Sid = "basic_content_list_building_guarded_resource_banks_tier_2", Name = "Охраняемые банки Т2" };
        public static readonly SidMapping GuardedBanksTier3 = new() { Sid = "basic_content_list_building_guarded_resource_banks_tier_3", Name = "Охраняемые банки Т3" };
        public static readonly SidMapping BasicStorageBanks = new() { Sid = "basic_content_list_basic_storage", Name = "Случайное базовое хранилище" };

        public static readonly SidMapping RandomRareMines = new() { Sid = "basic_content_list_rare_mines", Name = "Случайная редкая шахта" };
        public static readonly SidMapping RandomRareMinesBiomeRestricted = new() { Sid = "basic_content_list_rare_mines_by_biome", Name = "Случайная редкая шахта (по биому)" };
        public static readonly SidMapping RandomGuardedUnitBank = new() { Sid = "basic_content_list_building_guarded_units_banks", Name = "Случайный охраняемый банк существ" };
        public static readonly SidMapping HeroBuffTier1 = new() { Sid = "basic_content_list_building_hero_buff_tier_1", Name = "Случайное усиление героя Т1" };
        public static readonly SidMapping HeroExpTier2 = new() { Sid = "basic_content_list_building_hero_exp_tier_2", Name = "Случайный опыт героя Т2" };
        public static readonly SidMapping HeroStatsAndSkillsTier1 = new() { Sid = "basic_content_list_building_hero_stats_and_skills_tier_1", Name = "Случайная характеристика/навык героя Т1" };
        public static readonly SidMapping HeroStatsAndSkillsTier2 = new() { Sid = "basic_content_list_building_hero_stats_and_skills_tier_2", Name = "Случайная характеристика/навык героя Т2" };
        public static readonly SidMapping HeroStatsAndSkillsTier3 = new() { Sid = "basic_content_list_building_hero_stats_and_skills_tier_3", Name = "Случайная характеристика/навык героя Т3" };
        public static readonly SidMapping MagicBuildingsTier1 = new() { Sid = "basic_content_list_building_magic_tier_1", Name = "Случайная магич. постройка Т1" };
        public static readonly SidMapping MagicBuildingsTier2 = new() { Sid = "basic_content_list_building_magic_tier_2", Name = "Случайная магич. постройка Т2" };
        public static readonly SidMapping HeroImprovementUncommon = new() { Sid = "content_list_building_uncommon_hero_banks", Name = "Необычное развитие героя" };
        public static readonly SidMapping VisionBuildingsTier1 = new() { Sid = "basic_content_list_vision_buildings_tier_1", Name = "Случайная постройка обзора Т1" };
        public static readonly SidMapping RandomPickupItems = new() { Sid = "basic_content_list_pickup_random_items", Name = "Случайные предметы-подборы" };
        public static readonly SidMapping UtopiaBuildings = new() { Sid = "content_list_building_utopia", Name = "Утопия (Дракон/Руины/Лаб.)" };
        public static readonly SidMapping EpicGuardedResourceBanks = new() { Sid = "content_list_building_epic_guarded_resource_banks", Name = "Эпические охраняемые хранилища" };
        public static readonly SidMapping GuardedUnitBanksBiomeRestricted = new() { Sid = "basic_content_list_building_guarded_units_banks_only_biome_restriction", Name = "Охраняемый банк существ (по биому)" };
        public static readonly SidMapping GuardedUnitBanksNoBiome = new() { Sid = "basic_content_list_building_guarded_units_banks_no_biome_restriction", Name = "Охраняемый банк существ (без биома)" };
        public static readonly SidMapping MythicScrollBoxPickup = new() { Sid = "basic_content_list_pickup_mythic_scroll_box", Name = "Случайный мифический свиток" };
        public static readonly SidMapping PandoraBoxArmyLowTier = new() { Sid = "content_list_pickup_pandora_box_army_low_tier", Name = "Ящик Пандоры: армия (низкий ур.)" };
        public static readonly SidMapping PandoraBoxArmyHighTier = new() { Sid = "content_list_pickup_pandora_box_army_high_tier", Name = "Ящик Пандоры: армия (высокий ур.)" };
    }

    /// <summary>
    /// English display names for content items, keyed by SID. Lets the content combos localize
    /// without touching the ~150 definitions above (Russian stays in <see cref="SidMapping.Name"/>).
    /// </summary>
    public static class ContentNamesEn
    {
        public static readonly Dictionary<string, string> Map = new()
        {
            ["alchemy_lab"] = "Alchemy Lab",
            ["arena"] = "Arena",
            ["beer_fountain"] = "Beer Fountain",
            ["boreal_call"] = "Boreal Call",
            ["celestial_sphere"] = "Celestial Sphere",
            ["altar_of_magic_1"] = "Shrine of Night Shadow",
            ["altar_of_magic_2"] = "Shrine of Daylight",
            ["altar_of_magic_3"] = "Arcane Shrine",
            ["altar_of_magic_4"] = "Primordial Shrine",
            ["magic_amplifier_1"] = "Night Shadow Amplifier",
            ["magic_amplifier_2"] = "Daylight Amplifier",
            ["magic_amplifier_3"] = "Arcane Amplifier",
            ["magic_amplifier_4"] = "Primordial Amplifier",
            ["chimerologist"] = "Chimerologist",
            ["circus"] = "Circus",
            ["college_of_wonder"] = "College of Wonder",
            ["crystal_trail"] = "Crystal Trail",
            ["dragon_utopia"] = "Dragon Utopia",
            ["eternal_dragon"] = "Eternal Dragon",
            ["fickle_shrine"] = "Fickle Shrine",
            ["flattering_mirror"] = "Flattering Mirror",
            ["forge"] = "Forge",
            ["fort"] = "Fort",
            ["fountain"] = "Fountain",
            ["fountain_2"] = "Fountain 2",
            ["huntsmans_camp"] = "Huntsman's Camp",
            ["infernal_cirque"] = "Infernal Cirque",
            ["insaras_eye"] = "Insara's Eye",
            ["jousting_range"] = "Jousting Range",
            ["mana_well"] = "Mana Well",
            ["market"] = "Market",
            ["mine_crystals"] = "Crystal Vein",
            ["mine_gemstones"] = "Gemstone Hill",
            ["mine_gold"] = "Gold Mine",
            ["mine_mercury"] = "Mercury Rift",
            ["mine_ore"] = "Ore Pit",
            ["mine_wood"] = "Sawmill",
            ["mirage"] = "Mirage",
            ["mysterious_stone"] = "Mysterious Stone",
            ["mystical_tower"] = "Mystical Tower",
            ["scroll_box"] = "Magic Scroll",
            ["enchanted_scroll_box"] = "Enchanted Scroll",
            ["mythic_scroll_box"] = "Mythic Scroll",
            ["orb_observatory"] = "Orb Observatory",
            ["pandora_box"] = "Pandora's Box",
            ["petrified_memorial"] = "Petrified Memorial",
            ["pile_of_books"] = "Pile of Books",
            ["point_of_balance"] = "Point of Balance",
            ["prison"] = "Prison",
            ["quixs_path"] = "Quix's Path",
            ["random_hire_1"] = "Random Hire (Tier 1)",
            ["random_hire_2"] = "Random Hire (Tier 2)",
            ["random_hire_3"] = "Random Hire (Tier 3)",
            ["random_hire_4"] = "Random Hire (Tier 4)",
            ["random_hire_5"] = "Random Hire (Tier 5)",
            ["random_hire_6"] = "Random Hire (Tier 6)",
            ["random_hire_7"] = "Random Hire (Tier 7)",
            ["random_item_common"] = "Random Item (Common)",
            ["random_item_epic"] = "Random Item (Epic)",
            ["random_item_legendary"] = "Random Item (Legendary)",
            ["random_item_rare"] = "Random Item (Rare)",
            ["remote_foothold"] = "Remote Foothold",
            ["research_laboratory"] = "Research Laboratory",
            ["ritual_pyre"] = "Ritual Pyre",
            ["sacrificial_shrine"] = "Sacrificial Shrine",
            ["shady_den"] = "Shady Den",
            ["stables"] = "Stables",
            ["tavern"] = "Tavern",
            ["tear_of_truth"] = "Tear of Truth",
            ["the_gorge"] = "Pile of Carrion",
            ["town_gate"] = "Town Gate",
            ["tree_of_abundance"] = "Tree of Abundance",
            ["troglodyte_throne"] = "Troglodyte Throne",
            ["twilight_bloom"] = "Twilight Bloom",
            ["unforgotten_grave"] = "Unforgotten Grave",
            ["university"] = "University",
            ["unstable_ruins"] = "Unstable Ruins",
            ["watchtower"] = "Watchtower",
            ["wind_rose"] = "Wind Rose",
            ["wise_owl"] = "Wise Owl",
            ["storage_wood"] = "Wood Storage",
            ["storage_ore"] = "Ore Storage",
            ["storage_gold"] = "Gold Storage",
            ["storage_mercury"] = "Mercury Storage",
            ["storage_crystals"] = "Crystal Storage",
            ["storage_gemstones"] = "Gemstone Storage",
            ["storage_dust"] = "Dust Storage",
            ["gardener"] = "Gardener",
            ["windmill"] = "Windmill",
            ["village"] = "Village",
            ["gingerbread_house"] = "Gingerbread House",
            ["peasant_cart"] = "Peasant Cart",
            ["abandoned_corpse"] = "Abandoned Corpse",
            ["abandoned_mansion"] = "Abandoned Mansion",
            ["abnormal_structure"] = "Abnormal Structure",
            ["alvars_eye"] = "Alvar's Eye",
            ["black_tower"] = "Black Tower",
            ["circle_of_life"] = "Circle of Life",
            ["cursed_old_house"] = "Cursed Old House",
            ["crow_nest"] = "Crow's Nest",
            ["goblin_cache"] = "Goblin Cache",
            ["iridescent_abbey"] = "Iridescent Abbey",
            ["legions_memorial"] = "Legion's Memorial",
            ["mereas_shrine"] = "Merea's Shrine",
            ["monty_hall"] = "Monty Hall",
            ["overgrown_grave"] = "Overgrown Grave",
            ["prismatic_lair"] = "Prismatic Lair",
            ["raiders_camp"] = "Raiders' Camp",
            ["heros_crypt"] = "Hero's Crypt",
            ["uncanny_rite"] = "Uncanny Rite",
            ["learning_stone"] = "Learning Stone",
            ["lost_library"] = "Lost Library",
            ["tree_of_knowledge"] = "Tree of Knowledge",
            ["stinging_sword"] = "Stinging Sword",
            ["armory_automaton"] = "Armory Automaton",
            ["magic_wheel"] = "Magic Wheel",
            ["knowledge_garden"] = "Knowledge Garden",
            ["maze"] = "Maze",
            ["trial_scales"] = "Trial Scales",
            ["mercenary_guild"] = "Mercenary Guild",
            // ── Include lists (AuroraRMG's own grouping labels) ──
            ["content_list_building_random_hires_low_tier"]  = "Random Hire (Low Tier)",
            ["content_list_building_random_hires_high_tier"] = "Random Hire (High Tier)",
            ["basic_content_list_building_random_hires"]     = "Random Hire (Any Tier)",
            ["content_list_building_random_hires"]           = "Random Hire (Any Tier, weighted)",
            ["basic_content_list_building_guarded_resource_banks_tier_1"] = "Resource Banks T1",
            ["basic_content_list_building_guarded_resource_banks_tier_2"] = "Resource Banks T2",
            ["basic_content_list_building_guarded_resource_banks_tier_3"] = "Guarded Banks T3",
            ["basic_content_list_basic_storage"]             = "Random Basic Storage",
            ["basic_content_list_rare_mines"]                = "Random Rare Mine",
            ["basic_content_list_rare_mines_by_biome"]       = "Random Rare Mine (by biome)",
            ["basic_content_list_building_guarded_units_banks"] = "Random Guarded Unit Bank",
            ["basic_content_list_building_hero_buff_tier_1"] = "Random Hero Buff T1",
            ["basic_content_list_building_hero_exp_tier_2"]  = "Random Hero XP T2",
            ["basic_content_list_building_hero_stats_and_skills_tier_1"] = "Random Hero Stat/Skill T1",
            ["basic_content_list_building_hero_stats_and_skills_tier_2"] = "Random Hero Stat/Skill T2",
            ["basic_content_list_building_hero_stats_and_skills_tier_3"] = "Random Hero Stat/Skill T3",
            ["basic_content_list_building_magic_tier_1"]     = "Random Magic Building T1",
            ["basic_content_list_building_magic_tier_2"]     = "Random Magic Building T2",
            ["content_list_building_uncommon_hero_banks"]    = "Uncommon Hero Development",
            ["basic_content_list_vision_buildings_tier_1"]   = "Random Vision Building T1",
            ["basic_content_list_pickup_random_items"]       = "Random Pickup Items",
            ["content_list_building_utopia"]                 = "Utopia (Dragon/Ruins/Lab)",
            ["content_list_building_epic_guarded_resource_banks"] = "Epic Guarded Banks",
            ["basic_content_list_building_guarded_units_banks_only_biome_restriction"] = "Guarded Unit Bank (by biome)",
            ["basic_content_list_building_guarded_units_banks_no_biome_restriction"]   = "Guarded Unit Bank (no biome)",
            ["basic_content_list_pickup_mythic_scroll_box"]  = "Random Mythic Scroll",
            ["content_list_pickup_pandora_box_army_low_tier"]  = "Pandora's Box: army (low tier)",
            ["content_list_pickup_pandora_box_army_high_tier"] = "Pandora's Box: army (high tier)",
        };

        /// <summary>RU <see cref="SidMapping.Name"/> by default; the English name when <paramref name="english"/> and a translation exists.</summary>
        public static string Of(SidMapping m, bool english) =>
            english && Map.TryGetValue(m.Sid, out var en) && en.Length > 0 ? en : m.Name;
    }

    public static class GlobalContent
    {
        public static readonly IReadOnlyList<SidMapping> GlobalContentList =
            ContentIds.GetAll().Concat(IncludeListIds.GetAll()).ToList().AsReadOnly();

        public static SidMapping? GetBySid(string sid)
        {
            if (string.IsNullOrWhiteSpace(sid))
            {
                return null;
            }

            return GlobalContentList.FirstOrDefault(item =>
                string.Equals(item.Sid, sid));
        }
        public static SidMapping? GetByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            // Match the Russian name OR the English display name, so the "Add" path works
            // regardless of the current UI language.
            return GlobalContentList.FirstOrDefault(item =>
                string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase)
             || string.Equals(ContentNamesEn.Map.GetValueOrDefault(item.Sid), name, StringComparison.OrdinalIgnoreCase));
        }


    }
    internal static class SidReflection
    {
        internal static IReadOnlyList<SidMapping> GetSidMappings(Type sourceType)
        {
            return sourceType
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(field => field.FieldType == typeof(SidMapping))
                .Select(field => (SidMapping?)field.GetValue(null))
                .Where(mapping => mapping is not null)
                .Cast<SidMapping>()
                .ToList()
                .AsReadOnly();
        }
    }
}
