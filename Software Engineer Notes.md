# Software Engineer Notes

---

* Author - Fermin Cortell
* Created On - 20230804

---

## Index

[SOLID Principles](#solid-principles)
[Async vs await](#async-vs-await)

---

## Solid Principles

[Original source by LucasDiogo96](https://github.com/LucasDiogo96/S.O.L.I.D)

Open .NET solution [here](./assets/projects/solid/SolidExample.sln)

There are five principles to follow when we write code with object-oriented programming to make it more readable and maintainable if we don’t want that our program becomes a clock bomb in our hands. These principles together compose the acronym SOLID which means respectively.

* 1. [**S**ingle responsibility principle.](#single-responsibility-principle)

* 2. [**O**pen-closed principle.](#open-closed-principle)

* 3. [**L**iskov substitution principle.](#liskov-substitution-principle)

* 4. [**I**nterface segregation principle.](#interface-segregation-principle)

* 5. [**D**ependency Inversion Principle.](#dependency-inversion-principle)

### 1. **Single responsibility principle**

**A class should have one and only one reason to change.**

It means that a class must only be responsible for one context, that is, one thing, and what isn’t of its context will be out of the class.

Below there is an example where we have more than one responsible for our class.

#### 1.1. Problem

    ```cs
    /// This class represents a person
    public class Person
    {
            public int Id { get; set; }
            public string ITIN { get; set; }
            public string Name { get; set; }
            public DateTime BirthDate { get; set; }

            /// Verify if this data is valid
            public bool Validate()
            {
                return string.IsNullOrWhiteSpace(Name) && BirthDate == DateTime.MinValue && !ValidateITIN(ITIN);
            }

            /// Validate Individual Taxpayer Identification Number (ITIN)
            public static bool ValidateITIN(string document)
            {
                return new Regex(@"^(9\d{2})([ \-]?)([7]\d|8[0-8])([ \-]?)(\d{4})$").IsMatch(document);
            }

            /// Save person
            public void Save()
            {
                const string queueName = "Person";

                // Get rabbitMQ connection 
                IConnection connection = GetConnection();
                // Create Queue
                CreateQueue(queueName, connection);
                // Publish
                Publish(JsonConvert.SerializeObject(this), queueName, connection);
            }

            /// Get the RabbitMQ connection
            public IConnection GetConnection()
            {
                return new ConnectionFactory
                {
                    HostName = "localhost",
                    UserName = "xpto",
                    Password = "12345"

                }.CreateConnection();
            }

            /// Create the de queue
            public QueueDeclareOk CreateQueue(string queueName, IConnection connection)
            {
                QueueDeclareOk queue;
                using (var channel = connection.CreateModel())
                {
                    queue = channel.QueueDeclare(queueName, false, false, false, null);
                }
                return queue;
            }

            /// Publish the data
            public bool Publish(string message, string queueName, IConnection connection)
            {
                using (var channel = connection.CreateModel())
                {
                    channel.BasicPublish(string.Empty, queueName, null, Encoding.ASCII.GetBytes(message));
                }
                return true;
            }
    }
    ```

If we try to count it, we will realize that there are at least 5 responsibilities that shouldn’t be there. So, the person class not must know how to persist itself or how to validate an individual taxpayer identification or how to create and how to use the RabbitMQ as well.

#### 1.2. Solution

So we need to segregate each responsibility into different classes so if something change we know that we just need to change in that class, and it will not affect the other components, and it will make our code more testable and readable.

The responsibility of the person is to provide valid data..

    ```cs
    public class Person
    {
            public int Id { get; set; }
            public string ITIN { get; set; }
            public string Name { get; set; }
            public DateTime BirthDate { get; set; }

            public bool IsValid()
            {
                return !string.IsNullOrWhiteSpace(Name) && 
                       BirthDate != DateTime.MinValue && 
                      !IdentificationService.ValidateITIN(ITIN);
            }
    }
    ```

The identification service has only the responsibility to tell us if the ITIN is valid.

    ```cs
    public static class IdentificationService
    {
            public static bool ValidateITIN(string document)
            {
                return new Regex(@"^(9\d{2})([ \-]?)([7]\d|8[0-8])([ \-]?)(\d{4})$").IsMatch(document);
            }
    }
    ```

The EventConnectionFactory provides us with the connection.

    ```cs
    public class EventConnectionFactory
    {
            public IConnection GetConnection()
            {
                return new ConnectionFactory
                {
                    HostName = "localhost",
                    UserName = "xpto",
                    Password = "12345"

                }.CreateConnection();
            }
    }
    ```

The EventBusService will just only have the behavior of RabbitMQ.

    ```cs
    public class EventBusService
    {
            public QueueDeclareOk CreateQueue(string queueName, IConnection connection)
            {
                QueueDeclareOk queue;
                using (var channel = connection.CreateModel())
                {
                    queue = channel.QueueDeclare(queueName, false, false, false, null);
                }
                return queue;
            }

            public bool Publish(string message, string queueName, IConnection connection)
            {
                using (var channel = connection.CreateModel())
                {
                    channel.BasicPublish(string.Empty, queueName, null, Encoding.ASCII.GetBytes(message));
                }
                return true;
            }
    }
    ```

And finally, we should use a business class to create our business logic to persist the person data.

    ```cs
    public class PersonBusiness
    {
            const string queueName = "person";

            public bool Save(Person person)
            {
                if (person.IsValid())
                {
                    EventConnectionFactory connectionFactory = new EventConnectionFactory();

                    // Get rabbitMQ connection 
                    IConnection connection = connectionFactory.GetConnection();

                    EventBusService busService = new EventBusService();

                    // Create Queue
                    busService.CreateQueue(queueName, connection);

                    // Publish
                    busService.Publish(JsonConvert.SerializeObject(person), queueName, connection);

                    return true;
                }

                return false;
            }
     }
    ```

### 2. **Open-closed Principle**

**Objects or entities should be open for extension but closed for modification.**

A summary of it, it means that a class should be easily extendable without modifying the class itself.

#### 2.1. Problem

Let’s imagine we have a class called Check, and the context of this class is mark a presence.

    ```cs
    public class Check
    {
            public int Id { get; set; }
            public CheckTypeEnum Type { get; set; }
            public string Justification { get; set; }
            public DateTime EntryDate { get; set; }
            public DateTime ExitDate { get; set; }
    }
    ```

And here we have the type of this check if, basically if it’s a checking or a checkout.

    ```cs
    public enum CheckTypeEnum
    {
            [Description("Check-IN")]
            IN,    
            [Description("Check-Out")]
            OUT
    }
    ```

And when we try to do something with this data most of the programmers be based on the enum type, and if tomorrow there is a new type of check? Probably you are thinking… I just need to put a new enum type and write my code based on this kind of check.

    ```cs
    public void Save(Check check)
    {
          if (check.Type == CheckTypeEnum.IN)
          {         
               //DO SOMETHING
          }
          else if (check.Type == CheckTypeEnum.OUT)
          {
               //DO SOMETHING ELSE
          }
    }
    ```

It’s a bad practice of this because you will modify the code that was already working, and it can be dangerous, and also you won’t make a good unity test.

How to solve it?

#### 2.2. Solution

In this case, we will create an [abstract class](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/abstract) to isolate our business logic using an override for each kind of check.

    ```cs
    public abstract class CheckService
    {
        public abstract void CreateCheck(Check check);
    }
    ```

And here is the icing on the cake.

As I said above, we can isolate the business logic for each kind of check using inheritance, and we can write a new functionality without changing the existing code, and it will prevent situations like we are changing something in the class and needs to adapt out depending on the kind of class.

    ```cs
    public class Checkin : CheckService
    {
          public override void CreateCheck(Check check)
          {
               //our business rules for a checkout
          }
    }
    ```cs

    ```cs
    public class Checkout: CheckService
    {
         public override void CreateCheck(Check check)
         {
               //our business rules for a checkout
         }
    }  
    ```

### 3. **Liskov substitution principle**

**Let q(x) be a property provable about objects of x of type T. Then q(y) should be provable for objects y of type S where S is a subtype of T.**

If you read it, you can be confused, but translating it into the developers’ world it becomes.

**Derived classes must be substitutable for their base classes.**

It’s better now, right? 😄

#### 3.1. Problem

The following code approaches it.

We have a class **Apple**, this class has a virtual method that can be extendable for the subclasses.

    ```cs
    public class Apple 
    {
        public virtual string GetColor()
        {
           return "Red";
        }
    }
    ```

Here we have a class Orange that extends Apple and overrides the GetColor method.

    ```cs
    public class Orange: Apple
    {
        public override string GetColor() => "Orange";
    }
    ```

Pay attention to this situation. Theoretically, the Orange is an Apple. It extends that, and I can instantiate like the code below without problem because both are the same thing.

    ```cs
    Apple fruit = new Orange();
    Console.WriteLine("An apple is " + fruit.GetColor());
    ```

Now we will run the code below, and we will get the following result.

An apple is Orange

We must take care when we use inheritance because if the code is not respecting this principle the behavior of the functionality can be the opposite of what we want.

Let’s imagine that we are coding an application to throw a missile, in this case, we will throw it to the wrong place, and it can be expensive and harmful.

#### 3.2. Solution

In this case, we should have a generic class between both that we will call fruit. So, both implement an abstract class fruit and override the GetColor method.

    ```cs
    public abstract class Fruit
    {
        public abstract string GetColor();
    }

    public class Orange: Fruit
    {
         public override string GetColor() => "Orange";
    }

    public class Apple: Fruit
    {
         public override string GetColor() => "Red";
    }
    ```

An apple is red

Orange is Orange

### 4. **Interface segregation principle**

**A client should never be forced to implement an interface that it doesn’t use or clients shouldn’t be forced to depend on methods they do not use.**

Some programmers are very reluctant in a single interface instead of an interface per class, and it can be against the principle in the following case:

Here we have a single interface that has the signature of two methods.

    ```cs
    public interface IRegisterService
    {
        object Get(int Id);
        Task<object> SearchTaxId(string taxId);
    }
    ```cs

And finally, we have two classes that will implement it.

    ```cs
    public class CompanyService: IRegisterService
    {
            public static HttpClient client = new HttpClient();

            public object Get(int Id)
            {
                return new
                {
                    Id = Id,
                    Name = "COMPANY NAME",
                    TradeName = "COMPANY NAME LTDA",
                    IE = "668.284.686.980",
                    TaxId = "17.416.651/0001-60",
                    Email = "diretoria@ritaeedsonconstrucoesltda.com.br"
                };
            }

            public async Task<object> SearchTaxId(string taxId)
            {
                string token = "xpto";

                string Url = String.Format("https://www.sintegraws.com.br/api/v1/execute-api.php?token={0}&cnpj={1}&plugin=RF", token, taxId);

                var result = await client.GetAsync(Url);

                var content = await result.Content.ReadAsStringAsync();

                return JsonConvert.DeserializeObject(content);
            }
    }
    ```

Let’s pay attention to the code below if you look, a product doesn’t have a Tax ID, so we were forced to implement it to compile our code but not necessarily we have logic inside the method.

    ```cs
    public class ProductService: IRegisterService
    {
        public object Get(intidd)
        {
            return new
            {
                Id = Id,
                GTIN = "7891910000197",
                Price = 12.000,
                Name = "IPHONE XII"
            };
        }

        public async Task<object> SearchTaxId(string taxId)
        {
            throw new System.NotImplementedException();
        }
    }
    ```

To solve it, we just need to create an interface for the respective classes.

    ```cs
    public interface  IProductService
    {
            object Get(int Id);
    }

    public interface ICompanyService
    {
            object Get(int Id);
            Task<object> SearchTaxId(string cnpj);
    }
    ```

### 5. **Dependency Inversion Principle.**

**Entities must depend on abstractions, not on concretions. It states that the high-level module must not depend on the low-level module, but should depend on abstractions.**

This principle is about user interfaces and dependency injection instead use a class directly. For example, if you are implementing a database persistence, and today you use SQL Server but tomorrow you need to use the MongoDB instead, you w need to change it in the high-level code where it is being used instead of just in the base class because you depend on directly, basically we need to decrease coupling of our code.

#### 5.1. Problem

Here we have a class that uses the persistence of SQL Server.

    ```cs
    public class SqlServerRepository
    {
            public void Save(Person person)
            {
                string connectionString = "Server=myServerAddress;Database=myDataBase;User Id=myUsername;Password=myPassword;";

                string sql = "insert into Person(name) values (@name)";

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@name", person.Name);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
    }
    ```

And here we have the business that uses the persistence class, but if you observe it, the save method needs to instantiate the repository if it needs to use that.

    ```cs
    public class Business
    {
         public void Save(Person person)
         {
                SqlServerRepository persistence = new SqlServerRepository();

                persistence.Save(person);
          }
    }
    ```

But if I change my persistence to MongoDB per the example, I will need to rename each place where it’s used because of the class coupling.

    ```cs
    public class MongoDBRepository
    {
        public void Save(Person person)
        {
                const string connectionString = "mongodb://localhost:27017";

                var client = new MongoClient(connectionString);

                var database = client.GetDatabase("example");

                var collection = database.GetCollection<Person>("Person");

                collection.InsertOne(new Person { Name = person.Name });
        }
    }

    public class Business
    {
        public void Save(Person person)
        {
           MongoDBRepository persistence = new MongoDBRepository();

           persistence.Save(person);
        }
    }
    ```

#### 5.2. Solution

If we depend on abstractions, this database migration will be easier.

In the first step, we need to create an interface.

    ```cs
    public interface IRepository
    {
        void Save(Person person);
    }
    ```

And we just need to use inheritance.

    ```cs
    public class MongoDBRepository: IRepository
    ```

And here is the magic. We won’t need to instantiate the class by database if we need to change the kind of persistence we will change it only in one place, and it will be in the dependency injection container to refer to the new persistence.

    ```cs
    private readonly IRepository _repository;

    public class Business
    {
        public Business(IRepository repository)
        {
           _repository = repository;
        }

        public void Save(string name)
        {
           _repository.Save(name);
        }
    }
    ```

Let’s imagine the following situation.

You use the SqlServerPersistence in almost 50 classes in your application, so you will need to change the instance type in all of these places to use a new kind of persistence, with dependency injection if you depend on just an abstraction and use as Dependency injection you will need just to inherit of the same interface and change it in your DI container.

### References

**C# Best Practice: Dangers of Violating SOLID Principles in C#**: [https://docs.microsoft.com/en-us/archive/msdn-magazine/2014/may/csharp-best-practices-dangers-of-violating-solid-principles-in-csharp](https://docs.microsoft.com/en-us/archive/msdn-magazine/2014/may/csharp-best-practices-dangers-of-violating-solid-principles-in-csharp)

**Interfaces — define behavior for multiple types:** [https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/types/interfaces](https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/types/interfaces)

**Dependency injection in ASP.NET Core:** [https://docs.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-5.0](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-5.0)

**Solid Relevance:** [https://blog.cleancoder.com/uncle-bob/2020/10/18/Solid-Relevance.html](https://blog.cleancoder.com/uncle-bob/2020/10/18/Solid-Relevance.html)

**Coupling (computer programming):** [https://en.wikipedia.org/wiki/Coupling_(computer_programming)](https://en.wikipedia.org/wiki/Coupling_(computer_programming))

## Async vs await

Concepts studied keeping C# in mind.
