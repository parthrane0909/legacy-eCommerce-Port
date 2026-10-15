-- SUPERSEDED: this fashion-catalog seed is kept for history only.
-- The canonical seed is db/seed_generic.sql (generic multi-category catalog).
-- Do NOT run this file against LegacyEcommerceDb.
USE LegacyEcommerceDb;
GO
SET IDENTITY_INSERT dbo.Categories ON;
INSERT INTO dbo.Categories (Id, Name, Slug, Description, SortOrder) VALUES
(1, 'Shirts', 'shirts', 'Essential shirts and tees', 1),
(2, 'Jackets', 'jackets', 'Layering pieces', 2),
(3, 'Jeans', 'jeans', 'Denim, recut', 3),
(4, 'Outerwear', 'outerwear', 'Coats and overshirts', 4),
(5, 'Shoes', 'shoes', 'Footwear', 5);
SET IDENTITY_INSERT dbo.Categories OFF;
GO
SET IDENTITY_INSERT dbo.Products ON;
INSERT INTO dbo.Products (Id, Name, Slug, Description, Price, CategoryId, IsFeatured, IsActive) VALUES
(1, 'Velour Studio Tee — Washed Black', 'velour-studio-tee-washed-black', 'Heavyweight organic cotton tee with a washed-black finish.', 1200, 1, 1, 1),
(2, 'Oversized Boxy Shirt — Sand', 'oversized-boxy-shirt-sand', 'Relaxed boxy shirt in washed sand cotton.', 1490, 1, 0, 1),
(3, 'Field Jacket — Stone', 'field-jacket-stone', 'Utility field jacket with tonal hardware.', 4980, 2, 1, 1),
(4, 'Cropped Puffer — Off White', 'cropped-puffer-off-white', 'Lightweight cropped puffer for mild seasons.', 6280, 2, 1, 1),
(5, 'Selvedge Denim — Indigo', 'selvedge-denim-indigo', 'Straight-leg selvedge denim, raw indigo.', 3480, 3, 1, 1),
(6, 'Relaxed Chino Denim — Ecru', 'relaxed-chino-denim-ecru', 'Relaxed denim in washed ecru.', 2980, 3, 0, 1),
(7, 'Wool Overcoat — Camel', 'wool-overcoat-camel', 'Double-faced wool overcoat in camel.', 12800, 4, 1, 1),
(8, 'Rain Parka — Slate', 'rain-parka-slate', 'Water-resistant parka with storm hood.', 8980, 4, 0, 1),
(9, 'Low Runner — Off White', 'low-runner-off-white', 'Leather runner in off white.', 4980, 5, 1, 1),
(10, 'Suede Chelsea — Taupe', 'suede-chelsea-taupe', 'Suede chelsea boot, gum sole.', 7680, 5, 0, 1),
(11, 'Knit Polo — Ecru', 'knit-polo-ecru', 'Fine-gauge knit polo.', 2480, 1, 0, 1),
(12, 'Hooded Parka — Storm', 'hooded-parka-storm', 'Hooded parka for city weather.', 10580, 4, 1, 1);
SET IDENTITY_INSERT dbo.Products OFF;
GO
INSERT INTO dbo.ProductImages (ProductId, Url, AltText, IsPrimary, SortOrder)
SELECT Id, '/Content/images/products/' + CASE ((Id-1)%12)
 WHEN 0 THEN 'tshirt' WHEN 1 THEN 'shirt' WHEN 2 THEN 'jacket' WHEN 3 THEN 'outer'
 WHEN 4 THEN 'jeans' WHEN 5 THEN 'cap' WHEN 6 THEN 'blazer' WHEN 7 THEN 'boots'
 WHEN 8 THEN 'shoes' WHEN 9 THEN 'knit' WHEN 10 THEN 'hoodie' ELSE 'coat' END + '.jpg', Name, 1, 1 FROM dbo.Products;
INSERT INTO dbo.ProductImages (ProductId, Url, AltText, IsPrimary, SortOrder)
SELECT Id, '/Content/images/products/' + CASE ((Id-1)%12 + 6)
 WHEN 0 THEN 'tshirt' WHEN 1 THEN 'shirt' WHEN 2 THEN 'jacket' WHEN 3 THEN 'outer'
 WHEN 4 THEN 'jeans' WHEN 5 THEN 'cap' WHEN 6 THEN 'blazer' WHEN 7 THEN 'boots'
 WHEN 8 THEN 'shoes' WHEN 9 THEN 'knit' WHEN 10 THEN 'hoodie' ELSE 'coat' END + '.jpg', Name + ' — detail', 0, 2 FROM dbo.Products;
INSERT INTO dbo.ProductVariants (ProductId, Name, Sku, PriceDelta, Stock) SELECT Id, 'S', CONCAT('SKU-',Id,'-S'), 0, 20 FROM dbo.Products;
INSERT INTO dbo.ProductVariants (ProductId, Name, Sku, PriceDelta, Stock) SELECT Id, 'M', CONCAT('SKU-',Id,'-M'), 0, 20 FROM dbo.Products;
INSERT INTO dbo.ProductVariants (ProductId, Name, Sku, PriceDelta, Stock) SELECT Id, 'L', CONCAT('SKU-',Id,'-L'), 0, 20 FROM dbo.Products;
GO
