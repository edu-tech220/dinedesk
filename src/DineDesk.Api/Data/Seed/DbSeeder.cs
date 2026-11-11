using DineDesk.Api.Domain.Entities;
using DineDesk.Api.Domain.Enums;
using DineDesk.Api.Security;
using Microsoft.EntityFrameworkCore;

namespace DineDesk.Api.Data.Seed;

/// <summary>Seeds demo data on startup so the API and the Razor Pages client are usable out of the box.</summary>
public static class DbSeeder
{
    /// <summary>Seeds demo data into an already-created schema. Callers apply migrations
    /// (production, SQL Server) or <c>EnsureCreated</c> (tests, SQLite) beforehand.</summary>
    public static async Task SeedAsync(DineDeskDbContext db)
    {
        if (await db.Users.AnyAsync())
        {
            return;
        }

        var users = new[]
        {
            CreateUser("admin", "Admin User", "Admin#12345", UserRole.Admin),
            CreateUser("manager", "Morgan Manager", "Manager#12345", UserRole.Manager),
            CreateUser("server1", "Sam Server", "Server#12345", UserRole.Server),
            CreateUser("kitchen1", "Kai Kitchen", "Kitchen#12345", UserRole.Kitchen),
        };
        db.Users.AddRange(users);

        var bun = new Ingredient { Name = "Burger Bun", UnitOfMeasure = "pc", QuantityInStock = 50, ReorderThreshold = 10 };
        var pattyBeef = new Ingredient { Name = "Beef Patty", UnitOfMeasure = "pc", QuantityInStock = 40, ReorderThreshold = 10 };
        var chicken = new Ingredient { Name = "Chicken Breast", UnitOfMeasure = "pc", QuantityInStock = 30, ReorderThreshold = 8 };
        var cheddar = new Ingredient { Name = "Cheddar Cheese", UnitOfMeasure = "kg", QuantityInStock = 5, ReorderThreshold = 1 };
        var lettuce = new Ingredient { Name = "Lettuce", UnitOfMeasure = "kg", QuantityInStock = 4, ReorderThreshold = 1 };
        var tomato = new Ingredient { Name = "Tomato", UnitOfMeasure = "kg", QuantityInStock = 4, ReorderThreshold = 1 };
        var potato = new Ingredient { Name = "Fries Potato", UnitOfMeasure = "kg", QuantityInStock = 15, ReorderThreshold = 3 };
        var pasta = new Ingredient { Name = "Pasta", UnitOfMeasure = "kg", QuantityInStock = 10, ReorderThreshold = 2 };
        var tomatoSauce = new Ingredient { Name = "Tomato Sauce", UnitOfMeasure = "L", QuantityInStock = 8, ReorderThreshold = 2 };
        var colaSyrup = new Ingredient { Name = "Cola Syrup", UnitOfMeasure = "L", QuantityInStock = 6, ReorderThreshold = 1 };
        db.Ingredients.AddRange(bun, pattyBeef, chicken, cheddar, lettuce, tomato, potato, pasta, tomatoSauce, colaSyrup);

        var classicBurger = new MenuItem { Name = "Classic Burger", Category = "Burgers", Price = 8.99m };
        classicBurger.Recipe.Add(new MenuItemIngredient { Ingredient = bun, QuantityRequired = 1 });
        classicBurger.Recipe.Add(new MenuItemIngredient { Ingredient = pattyBeef, QuantityRequired = 1 });
        classicBurger.Recipe.Add(new MenuItemIngredient { Ingredient = cheddar, QuantityRequired = 0.05m });
        classicBurger.Recipe.Add(new MenuItemIngredient { Ingredient = lettuce, QuantityRequired = 0.05m });
        classicBurger.Recipe.Add(new MenuItemIngredient { Ingredient = tomato, QuantityRequired = 0.05m });

        var grilledChicken = new MenuItem { Name = "Grilled Chicken Sandwich", Category = "Burgers", Price = 9.49m };
        grilledChicken.Recipe.Add(new MenuItemIngredient { Ingredient = bun, QuantityRequired = 1 });
        grilledChicken.Recipe.Add(new MenuItemIngredient { Ingredient = chicken, QuantityRequired = 1 });
        grilledChicken.Recipe.Add(new MenuItemIngredient { Ingredient = lettuce, QuantityRequired = 0.05m });

        var cheeseFries = new MenuItem { Name = "Cheese Fries", Category = "Sides", Price = 4.50m };
        cheeseFries.Recipe.Add(new MenuItemIngredient { Ingredient = potato, QuantityRequired = 0.2m });
        cheeseFries.Recipe.Add(new MenuItemIngredient { Ingredient = cheddar, QuantityRequired = 0.05m });

        var spaghetti = new MenuItem { Name = "Spaghetti Bolognese", Category = "Pasta", Price = 10.99m };
        spaghetti.Recipe.Add(new MenuItemIngredient { Ingredient = pasta, QuantityRequired = 0.15m });
        spaghetti.Recipe.Add(new MenuItemIngredient { Ingredient = tomatoSauce, QuantityRequired = 0.1m });
        spaghetti.Recipe.Add(new MenuItemIngredient { Ingredient = pattyBeef, QuantityRequired = 0.1m });

        var cola = new MenuItem { Name = "Cola", Category = "Drinks", Price = 2.50m, IsHappyHourEligible = true };
        cola.Recipe.Add(new MenuItemIngredient { Ingredient = colaSyrup, QuantityRequired = 0.05m });

        db.MenuItems.AddRange(classicBurger, grilledChicken, cheeseFries, spaghetti, cola);

        db.DiningTables.AddRange(
            new DiningTable { Number = "T1", Capacity = 2 },
            new DiningTable { Number = "T2", Capacity = 2 },
            new DiningTable { Number = "T3", Capacity = 4 },
            new DiningTable { Number = "T4", Capacity = 4 },
            new DiningTable { Number = "T5", Capacity = 4 },
            new DiningTable { Number = "T6", Capacity = 6 },
            new DiningTable { Number = "T7", Capacity = 6 },
            new DiningTable { Number = "T8", Capacity = 2 });

        db.HappyHourWindows.Add(new HappyHourWindow
        {
            DayOfWeek = DayOfWeek.Monday,
            StartsAt = new TimeSpan(16, 0, 0),
            EndsAt = new TimeSpan(18, 0, 0),
            DiscountPercentage = 0.2m,
        });
        foreach (var day in new[] { DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday })
        {
            db.HappyHourWindows.Add(new HappyHourWindow
            {
                DayOfWeek = day,
                StartsAt = new TimeSpan(16, 0, 0),
                EndsAt = new TimeSpan(18, 0, 0),
                DiscountPercentage = 0.2m,
            });
        }

        await db.SaveChangesAsync();
    }

    private static User CreateUser(string username, string displayName, string password, UserRole role)
    {
        var (hash, salt) = PasswordHasher.HashPassword(password);
        return new User
        {
            Username = username,
            DisplayName = displayName,
            PasswordHash = hash,
            PasswordSalt = salt,
            Role = role,
        };
    }
}
