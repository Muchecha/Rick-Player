namespace Rick_Player.Main.Data;

public class Client
{
    public Client(string id, string name)
    {
        Id = id;
        Name = name;
    }

    public string Id { get; private set; }
    public string Name { get; set; }
}