namespace Ecommerce.Infrastructure.Seeding;

/// <summary>
/// Curated catalogue content.
///
/// Category hierarchy, product names, slugs, descriptions, prices, discounts, ratings, stock and
/// image associations are ported from the legacy canonical seed
/// <c>db/seed_generic.sql</c>. Three additional products were added to satisfy the assignment's
/// minimum of 25 (the legacy seed has 22); they are genuine catalogue items, not duplicates.
/// </summary>
internal static class SeedData
{
    internal sealed record CategorySeed(string Name, string Slug, string Description, int SortOrder, string? ParentSlug);

    internal sealed record ImageSeed(string Url, string Alt);

    internal sealed record VariantSeed(string Name, decimal PriceDelta);

    internal sealed record ProductSeed(
        string Name,
        string Slug,
        string Description,
        decimal Price,
        int Stock,
        string ImageUrl,
        string CategorySlug,
        int? DiscountPercent,
        decimal? Rating,
        bool IsFeatured,
        int AgeInDays,
        IReadOnlyList<ImageSeed> Images,
        IReadOnlyList<VariantSeed> Variants);

    private const string Img = "/images/products/";

    internal static readonly CategorySeed[] Categories =
    [
        new("Electronics", "electronics", "Gadgets, computers and audio gear", 1, null),
        new("Home & Living", "home-living", "Furniture, kitchen and decor", 2, null),
        new("Books", "books", "Fiction, technology and business reads", 3, null),
        new("Sports & Fitness", "sports-fitness", "Training gear and outdoor essentials", 4, null),
        new("Office Supplies", "office-supplies", "Stationery, bags and desk essentials", 5, null),
        new("Gaming", "gaming", "Controllers, headsets and accessories", 6, null),
        new("Beauty & Personal Care", "beauty-personal-care", "Skin, hair and everyday care", 7, null),

        new("Mobile Phones", "mobile-phones", "Phones and wearables", 1, "electronics"),
        new("Laptops", "laptops", "Laptops and desk gear", 2, "electronics"),
        new("Audio", "audio", "Headphones and speakers", 3, "electronics"),
        new("Accessories", "accessories", "Small tech accessories", 4, "electronics"),
        new("Furniture", "furniture", "Chairs, tables and seating", 1, "home-living"),
        new("Kitchen", "kitchen", "Brewing and cooking essentials", 2, "home-living"),
        new("Decor", "decor", "Lighting and decorative pieces", 3, "home-living"),
        new("Storage", "storage", "Organizers and storage", 4, "home-living"),
        new("Fiction", "fiction", "Novels and stories", 1, "books"),
        new("Technology", "technology", "Programming and tech books", 2, "books"),
        new("Business", "business", "Startups, finance and careers", 3, "books"),
        new("Education", "education", "Learning and reference", 4, "books"),
        new("Fitness", "fitness", "Training and yoga gear", 1, "sports-fitness"),
        new("Outdoor", "outdoor", "Bottles, bags and trail gear", 2, "sports-fitness"),
        new("Stationery", "stationery", "Notebooks, pens and paper", 1, "office-supplies"),
        new("Bags", "bags", "Backpacks and travel bags", 2, "office-supplies"),
        new("Controllers", "controllers", "Game controllers", 1, "gaming"),
        new("Game Accessories", "game-accessories", "Headsets and add-ons", 2, "gaming"),
        new("Skin Care", "skin-care", "Serums, creams and cleansers", 1, "beauty-personal-care"),
        new("Hair Care", "hair-care", "Shampoo, oils and styling", 2, "beauty-personal-care"),
    ];

    private static ImageSeed[] Img2(string file, string name) =>
        [new(Img + file + ".jpg", name), new(Img + "placeholder.jpg", name + " — alternate view")];

    internal static readonly ProductSeed[] Products =
    [
        new("Wireless Headphones Pro", "wireless-headphones-pro",
            "Over-ear Bluetooth headphones with active noise cancellation and 40-hour battery life.",
            4999m, 40, Img + "headphones.jpg", "audio", 15, 4.50m, true, 320,
            Img2("headphones", "Wireless Headphones Pro"),
            [new("Black", 0m), new("Silver", 200m)]),

        new("Mechanical Keyboard RGB", "mechanical-keyboard-rgb",
            "Hot-swappable mechanical keyboard with per-key RGB and a solid aluminium frame.",
            3499m, 25, Img + "keyboard.jpg", "accessories", null, 4.70m, false, 318,
            Img2("keyboard", "Mechanical Keyboard RGB"),
            [new("Brown Switch", 0m), new("Blue Switch", 150m)]),

        new("Bluetooth Speaker Mini", "bluetooth-speaker-mini",
            "Compact portable speaker with deep bass and 12-hour playtime. Splash resistant.",
            1999m, 60, Img + "speaker.jpg", "audio", 10, 4.30m, false, 315,
            Img2("speaker", "Bluetooth Speaker Mini"),
            [new("Standard", 0m)]),

        new("Ergonomic Wireless Mouse", "ergonomic-wireless-mouse",
            "Silent-click ergonomic mouse with adjustable DPI and 18-month battery.",
            1299m, 80, Img + "mouse.jpg", "accessories", null, 4.40m, false, 310,
            Img2("mouse", "Ergonomic Wireless Mouse"),
            [new("Graphite", 0m)]),

        new("Smart Watch Series 5", "smart-watch-series-5",
            "AMOLED display, heart-rate and sleep tracking, 10-day battery, 5ATM water resistance.",
            7999m, 30, Img + "watch.jpg", "accessories", 20, 4.60m, true, 305,
            Img2("watch", "Smart Watch Series 5"),
            [new("42mm", 0m), new("46mm", 1200m)]),

        new("Aluminium Laptop Stand", "aluminium-laptop-stand",
            "Foldable laptop stand with adjustable height and improved airflow design.",
            1799m, 50, Img + "stand.jpg", "laptops", null, 4.20m, false, 300,
            Img2("stand", "Aluminium Laptop Stand"),
            [new("Single", 0m)]),

        new("Minimal Desk Lamp", "minimal-desk-lamp",
            "Dimmable LED desk lamp with warm-to-cool temperature control and USB charging port.",
            1499m, 45, Img + "lamp.jpg", "decor", null, 4.50m, true, 296,
            Img2("lamp", "Minimal Desk Lamp"),
            [new("White", 0m), new("Black", 100m)]),

        new("Pour-Over Coffee Set", "pour-over-coffee-set",
            "Six-piece pour-over brewing kit with dripper, carafe, filters and measuring spoon.",
            2299m, 35, Img + "coffee.jpg", "kitchen", null, 4.80m, true, 292,
            Img2("coffee", "Pour-Over Coffee Set"),
            [new("Ceramic", 0m), new("Glass", 400m)]),

        new("Modern Lounge Chair", "modern-lounge-chair",
            "Ergonomic lounge chair with solid wood legs and washable fabric upholstery.",
            8999m, 12, Img + "chair.jpg", "furniture", 10, 4.40m, false, 288,
            Img2("chair", "Modern Lounge Chair"),
            [new("Charcoal", 0m), new("Sand", 0m)]),

        new("Kitchen Storage Organizer", "kitchen-storage-organizer",
            "Stackable BPA-free storage set with airtight lids for pantry organization.",
            999m, 70, Img + "kitchen.jpg", "storage", null, 4.10m, false, 284,
            Img2("kitchen", "Kitchen Storage Organizer"),
            [new("Set of 6", 0m)]),

        new("Classic Alarm Clock", "classic-alarm-clock",
            "Silent-sweep analog alarm clock with gentle wake-up chime and night light.",
            899m, 55, Img + "clock.jpg", "decor", 5, 4.00m, false, 280,
            Img2("clock", "Classic Alarm Clock"),
            [new("Ivory", 0m)]),

        new("Clean Code Handbook", "clean-code-handbook",
            "A practical guide to writing readable, maintainable software with real examples.",
            799m, 100, Img + "book1.jpg", "technology", null, 4.90m, true, 276,
            Img2("book1", "Clean Code Handbook"),
            [new("Paperback", 0m), new("Hardcover", 350m)]),

        new("The Startup Playbook", "the-startup-playbook",
            "Lessons on launching, funding and scaling a company from founders who did it.",
            649m, 80, Img + "books.jpg", "business", null, 4.60m, true, 272,
            Img2("books", "The Startup Playbook"),
            [new("Paperback", 0m)]),

        new("Mystery Novel Collection", "mystery-novel-collection",
            "Three bestselling mystery novels in one box set. Over 900 pages of suspense.",
            999m, 60, Img + "books.jpg", "fiction", 12, 4.30m, false, 268,
            Img2("books", "Mystery Novel Collection"),
            [new("Box Set", 0m)]),

        new("Home Gym Dumbbell Set", "home-gym-dumbbell-set",
            "Adjustable dumbbell pair from 2.5 to 24 kg with anti-roll coated handles.",
            5999m, 20, Img + "gym.jpg", "fitness", 15, 4.50m, false, 264,
            Img2("gym", "Home Gym Dumbbell Set"),
            [new("24 kg", 0m), new("32 kg", 2500m)]),

        new("Insulated Water Bottle 1L", "insulated-water-bottle-1l",
            "Double-wall vacuum bottle keeps drinks cold 24h or hot 12h. Leak-proof cap.",
            899m, 90, Img + "bottle.jpg", "outdoor", 8, 4.40m, false, 260,
            Img2("bottle", "Insulated Water Bottle 1L"),
            [new("Steel", 0m), new("Matte Black", 50m)]),

        new("Pro Yoga Mat 6mm", "pro-yoga-mat-6mm",
            "Non-slip TPE yoga mat with alignment lines and carry strap. Extra cushioning.",
            1299m, 65, Img + "mat.jpg", "fitness", null, 4.70m, true, 256,
            Img2("mat", "Pro Yoga Mat 6mm"),
            [new("Slate", 0m), new("Plum", 0m)]),

        new("Premium Hardcover Notebook", "premium-hardcover-notebook",
            "192 pages of 100 GSM dotted paper with lay-flat binding and ribbon marker.",
            499m, 120, Img + "notebook.jpg", "stationery", 20, 4.60m, false, 252,
            Img2("notebook", "Premium Hardcover Notebook"),
            [new("A5 Dotted", 0m), new("A4 Lined", 200m)]),

        new("Travel Laptop Backpack", "travel-laptop-backpack",
            "Water-resistant 25L backpack with padded 15.6 inch sleeve and USB passthrough.",
            2499m, 40, Img + "backpack.jpg", "bags", 15, 4.70m, true, 248,
            Img2("backpack", "Travel Laptop Backpack"),
            [new("Black", 0m), new("Navy", 0m)]),

        new("Pro Gaming Headset", "pro-gaming-headset",
            "7.1 surround gaming headset with noise-cancelling mic and memory-foam earcups.",
            3999m, 35, Img + "headset.jpg", "game-accessories", 10, 4.50m, false, 244,
            Img2("headset", "Pro Gaming Headset"),
            [new("Black", 0m), new("White", 300m)]),

        new("Wireless Game Controller", "wireless-game-controller",
            "Low-latency wireless controller with hall-effect sticks and 20-hour battery.",
            2999m, 28, Img + "controller.jpg", "controllers", null, 4.60m, true, 240,
            Img2("controller", "Wireless Game Controller"),
            [new("Standard", 0m), new("Elite", 1200m)]),

        new("Vitamin C Face Serum", "vitamin-c-face-serum",
            "15 percent vitamin C serum with hyaluronic acid for brighter, even-toned skin.",
            799m, 75, Img + "skincare.jpg", "skin-care", 20, 4.40m, false, 236,
            Img2("skincare", "Vitamin C Face Serum"),
            [new("30 ml", 0m), new("50 ml", 450m)]),

        // ---- Additional products added to reach the assignment minimum of 25 ----

        new("Studio Microphone USB", "studio-microphone-usb",
            "Cardioid condenser microphone with a built-in shock mount for streaming and podcasting.",
            5499m, 22, Img + "headphones.jpg", "audio", null, 4.50m, false, 232,
            Img2("headphones", "Studio Microphone USB"),
            [new("Cardioid", 0m)]),

        new("Adjustable Standing Desk", "adjustable-standing-desk",
            "Dual-motor sit-stand desk with memory presets and a 120 kg load rating.",
            18999m, 10, Img + "chair.jpg", "furniture", 25, 4.30m, true, 228,
            Img2("chair", "Adjustable Standing Desk"),
            [new("120 cm", 0m), new("140 cm", 1200m)]),

        new("Trail Running Shoes", "trail-running-shoes",
            "Aggressive-grip trail shoes with a rock plate and quick-dry upper for mixed terrain.",
            6499m, 26, Img + "watch.jpg", "outdoor", null, 4.40m, false, 224,
            Img2("watch", "Trail Running Shoes"),
            [new("UK 8", 0m), new("UK 9", 0m), new("UK 10", 0m)]),
    ];
}