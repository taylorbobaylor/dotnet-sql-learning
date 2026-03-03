# SOLID Principles

SOLID is almost always asked in senior developer interviews. You need to be able to define each principle AND give a concrete example in C#.

---

## S — Single Responsibility Principle

> *"A class should have one, and only one, reason to change."*

A class should do one thing and do it well. If you have to change a class for two different reasons, it's doing too much.

```csharp
// ❌ Violates SRP — this class saves to DB AND sends email AND generates report
public class OrderProcessor
{
    public void Process(Order order)
    {
        // Save to database
        _db.Orders.Add(order);
        _db.SaveChanges();

        // Send confirmation email
        var smtp = new SmtpClient();
        smtp.Send(order.CustomerEmail, "Order confirmed", $"Order #{order.Id} received");

        // Generate PDF report
        var pdf = new PdfGenerator();
        pdf.Generate(order);
    }
}

// ✅ SRP — each class has one job
public class OrderRepository  { public void Save(Order order) { ... } }
public class OrderEmailService { public void SendConfirmation(Order order) { ... } }
public class OrderReportService { public void GeneratePdf(Order order) { ... } }

public class OrderProcessor
{
    public OrderProcessor(OrderRepository repo, OrderEmailService email, OrderReportService reports) { ... }

    public void Process(Order order)
    {
        _repo.Save(order);
        _email.SendConfirmation(order);
        _reports.GeneratePdf(order);
    }
}
```

**Why it matters:** Smaller, focused classes are easier to test, maintain, and reason about.

---

## O — Open/Closed Principle

> *"Software entities should be open for extension, but closed for modification."*

You should be able to add new behavior without changing existing tested code. Use abstractions (interfaces/abstract classes) and add new implementations.

```csharp
// ❌ Violates OCP — adding a new payment type requires modifying this class
public class PaymentProcessor
{
    public void Process(Payment payment)
    {
        if (payment.Type == "CreditCard")  { ProcessCreditCard(payment); }
        else if (payment.Type == "PayPal") { ProcessPayPal(payment); }
        else if (payment.Type == "Crypto") { ProcessCrypto(payment); } // ← modified existing code
    }
}

// ✅ OCP — add new types by creating a new class, never touching existing code
public interface IPaymentHandler
{
    bool CanHandle(string paymentType);
    void Process(Payment payment);
}

public class CreditCardHandler : IPaymentHandler
{
    public bool CanHandle(string type) => type == "CreditCard";
    public void Process(Payment payment) { /* credit card logic */ }
}

public class PayPalHandler : IPaymentHandler
{
    public bool CanHandle(string type) => type == "PayPal";
    public void Process(Payment payment) { /* paypal logic */ }
}

// New crypto payment? Just add CryptoHandler : IPaymentHandler — no existing code changes
public class PaymentProcessor
{
    private readonly IEnumerable<IPaymentHandler> _handlers;
    public void Process(Payment payment)
    {
        var handler = _handlers.First(h => h.CanHandle(payment.Type));
        handler.Process(payment);
    }
}
```

---

## L — Liskov Substitution Principle

> *"Objects of a derived class should be substitutable for objects of the base class without breaking the program."*

If class `B` extends class `A`, you should be able to use `B` anywhere `A` is expected and the behavior should still be correct.

```csharp
// ❌ Classic LSP violation — Square pretends to be a Rectangle but breaks behavior
public class Rectangle
{
    public virtual int Width  { get; set; }
    public virtual int Height { get; set; }
    public int Area() => Width * Height;
}

public class Square : Rectangle
{
    public override int Width  { set { base.Width  = value; base.Height = value; } }
    public override int Height { set { base.Height = value; base.Width  = value; } }
}

// This code "works" for Rectangle but breaks for Square:
void SetDimensions(Rectangle r)
{
    r.Width = 4;
    r.Height = 5;
    Console.WriteLine(r.Area());  // Rectangle: 20. Square: 25. Broken!
}

// ✅ Fix — use a shared interface without the breaking hierarchy
public interface IShape { int Area(); }
public class Rectangle : IShape { ... }
public class Square    : IShape { ... }
```

**Practical takeaway:** Be careful with inheritance. LSP is often violated when subclasses override methods and change behavior in unexpected ways. Prefer composition over inheritance when in doubt.

---

## I — Interface Segregation Principle

> *"Clients should not be forced to depend on interfaces they do not use."*

Don't create "fat" interfaces. Split them into smaller, focused ones. Classes only implement what they need.

```csharp
// ❌ Fat interface — not every printer can scan or fax
public interface IPrinter
{
    void Print(Document doc);
    void Scan(Document doc);    // ← SimplePrinter doesn't support this
    void Fax(Document doc);     // ← SimplePrinter doesn't support this either
}

public class SimplePrinter : IPrinter
{
    public void Print(Document doc) { /* ok */ }
    public void Scan(Document doc)  { throw new NotImplementedException(); }  // ← forced!
    public void Fax(Document doc)   { throw new NotImplementedException(); }  // ← forced!
}

// ✅ Segregated interfaces — each class only implements what it needs
public interface IPrinter  { void Print(Document doc); }
public interface IScanner  { void Scan(Document doc);  }
public interface IFax      { void Fax(Document doc);   }

public class SimplePrinter : IPrinter { ... }
public class AllInOnePrinter : IPrinter, IScanner, IFax { ... }
```

---

## D — Dependency Inversion Principle

> *"Depend on abstractions, not concretions. High-level modules should not depend on low-level modules."*

Your business logic should depend on interfaces, not specific implementations. This enables testing (mock the interface) and flexibility (swap implementations).

```csharp
// ❌ Depends on concrete class — can't test without a real database
public class OrderService
{
    private readonly SqlOrderRepository _repository = new SqlOrderRepository();  // ← tight coupling

    public Order GetOrder(int id) => _repository.GetById(id);
}

// ✅ Depends on abstraction — testable, swappable
public interface IOrderRepository
{
    Order GetById(int id);
    void Save(Order order);
}

public class SqlOrderRepository : IOrderRepository { ... }     // Production
public class InMemoryOrderRepository : IOrderRepository { ... } // Tests

public class OrderService
{
    private readonly IOrderRepository _repository;

    public OrderService(IOrderRepository repository)  // ← injected via DI
    {
        _repository = repository;
    }

    public Order GetOrder(int id) => _repository.GetById(id);
}
```

DIP is why Dependency Injection (the next topic) exists — DI is the mechanism that makes DIP practical.

---

## SOLID Quick Reference Card

| Letter | Principle | One-line summary |
|---|---|---|
| **S** | Single Responsibility | One class, one job |
| **O** | Open/Closed | Extend with new code, don't modify old code |
| **L** | Liskov Substitution | Subclasses must honor the parent's contract |
| **I** | Interface Segregation | Many small interfaces over one fat interface |
| **D** | Dependency Inversion | Depend on interfaces, not concrete classes |
