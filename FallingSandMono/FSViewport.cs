using Godot;
using static Godot.GD;
using System;
using System.Collections.Generic;

public partial class FSViewport : Node2D
{
	[Export] public FastNoiseLite[] Noises;
	[Export] public Gradient Colors;


	[Export] public int GridSize = 16;
	[Export] public int TileSize = 16;
	[Export] public int TicksPerSecond = 30;
	[Export] public Color BackgroundColor = new Color(0f, 0f, 0f);
	private ParticleBase[,] table = new ParticleBase[0,0];
	private ParticleBase[] ParticleTypes = {new ParticleStone(), new ParticleSand(), new ParticleWater(), new ParticleOil(), new ParticleFire()};
	private Timer TickTimer = new Timer();

	public bool LeftPressed = false;
	public bool RightPressed = false;

	public int CurrentSelectionIndex = 0;
	public override void _Ready()
	{

		init_table();
		// init_table_with_noise();
	}

	public void init_table()
	{
		table = new ParticleBase[GridSize+1, GridSize+1];

		AddChild(TickTimer);
		TickTimer.OneShot = true;
		TickTimer.Start(1 / (float)TicksPerSecond);
		TickTimer.Timeout += Tick;

	}

	public void init_table_with_noise() {
		table = new ParticleBase[GridSize+1, GridSize+1];
		for (int x = 0; x < GridSize; x++) {
			for (int y = 0; y < GridSize; y++) {
				float noiseval = 0f;
				for (int index = 0; index < Noises.GetLength(0); index++) {
					FastNoiseLite noise = (FastNoiseLite)Noises[index];
					noiseval += (float)noise.GetNoise2D(x, y) / (1+index);
				}
				if (noiseval > 0.2f) {

					ParticleStone news = new ParticleStone();
					CreateParticleAt(x, y, news, new Vector2(0, 0));
					news.ParticleColor = Colors.Sample(Mathf.Abs(noiseval));
				}				
			}
		}
		AddChild(TickTimer);
		TickTimer.OneShot = true;
		TickTimer.Start(1 / (float)TicksPerSecond);
		TickTimer.Timeout += Tick;

	}

	public override void _Input(InputEvent @event) {
		if (@event is InputEventKey keyEvent && keyEvent.Pressed) {
			int num_key_pressed = @event.AsText().ToInt();
			CurrentSelectionIndex = num_key_pressed;
		}

		if (@event is InputEventMouseButton mouseEvent)
			{
				switch (mouseEvent.ButtonIndex)
				{
					case MouseButton.Left:
						LeftPressed = mouseEvent.Pressed;
						break;
					case MouseButton.Right:
						RightPressed = mouseEvent.Pressed;
						break;
				}
			}

	}


	public void Tick() {
		QueueRedraw();

		Vector2 MousePosition = GetLocalMousePosition() / TileSize;
		Vector2I LocalMousePosition = new Vector2I((int) Mathf.Round(MousePosition.X), (int) Mathf.Round(MousePosition.Y) );
		ParticleBase Particle = GetParticle(LocalMousePosition.X, LocalMousePosition.Y);

		if (LeftPressed && Particle == null) CreateParticleAt(LocalMousePosition.X, LocalMousePosition.Y, (ParticleBase)ParticleTypes[Mathf.Clamp(CurrentSelectionIndex-1, 0, ParticleTypes.GetLength(0))].Duplicate(), new Vector2(0f, 0f));

		if (RightPressed && Particle != null) {SetParticle(LocalMousePosition.X, LocalMousePosition.Y, null);}


		TickTimer.Start(1f / (float)TicksPerSecond);
	}


	public override void _Draw()
	{
		Rect2 BGR = new Rect2(new Vector2(0, 0), new Vector2(GridSize * TileSize, GridSize * TileSize));
		DrawRect(BGR, BackgroundColor);


		for (int x = 0; x < GridSize; x++) 
		{
			for (int y = 0; y < GridSize; y++) 
			{
				ParticleBase Particle = table[x, y];
				if (IsInstanceValid(Particle)) {
					if (Particle.HasMethod("physics"))
					{
						Particle.CallDeferred("physics");
					}
					
					Particle.CallDeferred("ParticleUpdate");

					Rect2 r = new Rect2(new Vector2(x * TileSize - TileSize / 2, y * TileSize - TileSize / 2), new Vector2(TileSize, TileSize));
					DrawRect(r, Particle.ParticleColor * new Color(.8f, .8f, .8f));
					// r = new Rect2(new Vector2(x * TileSize, y * TileSize), new Vector2(TileSize, TileSize));
					// DrawRect(r, Particle.ParticleColor, false, 2);
				}
			}
		}

	}




	public void CreateParticleAt(int x, int y, ParticleBase Particle, Vector2 ParticleVelocity) 
	{
		SetParticle(x, y, Particle);
		if (Particle.HasMethod("init"))
		{
		Particle.Call("init");
		Particle.viewport = this;
		}
		Particle.ParticleInit();
	
	}
	public void SetParticle(int x, int y, ParticleBase Particle) { 
		bool toDelete = false;
		if (x > 0 && x < GridSize-1) {
			if (x > 0 && y < GridSize-1) {
			table[x, y] = Particle;
			if (IsInstanceValid(Particle)) {
				Particle.ParticleCellPosition = new Vector2I(x, y);
				}
			} else toDelete = true;
		} else toDelete = true;
		if (toDelete) {
			Particle.QueueFree();
			// table[x, y] = null;
		}
	}

	public ParticleBase GetParticle(int x, int y) { 
		if (x > 0 && x < GridSize) {
			if (y > 0 && y < GridSize) {
				return table[x, y];
			}	
		}
		return null;

	}



}



public partial class ParticleBase : Node
{
	public Color ParticleColor = new Color(0f, 0f, 0f);
	public Color DefaultParticleColor = new Color(0f, 0f, 0f);
	public Vector2I ParticleCellPosition = new Vector2I(0, 0);
	public Vector2 ParticleVelocity = new Vector2(0, 0);
	public float ParticleTemperature = 15f;
	public float ParticleMeltingTemperature = 1;
	public bool CanMelt = false;
	public float ParticleTempDecay = 1.2f;
	public float ParticleStrength = 0f;
	public float Decay = 0f;
	public float Density = 0f;

	public FSViewport viewport = null;

	public bool IsFlammable = false;

	public void ParticleInit() {
		DefaultParticleColor = ParticleColor;
	}
	public void ParticleUpdate() {
		float ColorValue = (ParticleTemperature / ParticleMeltingTemperature) / 100f;
		if (ParticleTemperature > 12) {
			ParticleTemperature -= ParticleTempDecay;
		}
		if (CanMelt) {
			ParticleColor = DefaultParticleColor + new Color(ColorValue, 0f, 0f);
		}
	}

}


public partial class ParticlePowder : ParticleBase
{
	public void physics() {
		
		ParticleBase cell_below = viewport.GetParticle(ParticleCellPosition.X, ParticleCellPosition.Y + 1);
		ParticleBase cell_left = viewport.GetParticle(ParticleCellPosition.X - 1, ParticleCellPosition.Y + 1);
		ParticleBase cell_right = viewport.GetParticle(ParticleCellPosition.X + 1, ParticleCellPosition.Y + 1);
		
		bool canMoveDown = (cell_below == null);
		
		if (cell_below != null) {
			canMoveDown = cell_below.Density < Density;
		}

		bool canMoveLeft = (cell_left == null);
		
		if (cell_left != null) {
			canMoveLeft = cell_left.Density < Density;
		}

		bool canMoveRight = (cell_right == null);
		
		if (cell_right != null) {
			canMoveRight = cell_right.Density < Density;
		}

		if (canMoveDown) ParticleVelocity.Y += 1;
		else {
			if (canMoveLeft) ParticleVelocity.X = -1;
			if (canMoveRight) ParticleVelocity.X = 1;
		}
		if (ParticleVelocity != new Vector2(0f, 0f))
		{

			Vector2 VelocityVector = new Vector2(Mathf.Sign(ParticleVelocity.X), Mathf.Sign(ParticleVelocity.Y));
			ParticleBase ParticleInVelPlusPos = viewport.GetParticle((int)(ParticleCellPosition.X + VelocityVector.X), (int)(ParticleCellPosition.Y + VelocityVector.Y));
			if (ParticleInVelPlusPos == null) {
				viewport.SetParticle((int)(ParticleCellPosition.X), (int)(ParticleCellPosition.Y), null);
				viewport.SetParticle((int)(ParticleCellPosition.X + VelocityVector.X), (int)(ParticleCellPosition.Y + VelocityVector.Y), this);
			}
			else
			{
				if (ParticleInVelPlusPos is ParticleLiquid) 
				{
					viewport.SetParticle((int)(ParticleCellPosition.X), (int)(ParticleCellPosition.Y), ParticleInVelPlusPos);
					viewport.SetParticle((int)(ParticleCellPosition.X + VelocityVector.X), (int)(ParticleCellPosition.Y + VelocityVector.Y), this);
				}
			}

			ParticleVelocity -= VelocityVector;
		}
	}
}

public partial class ParticleLiquid : ParticleBase
{
	public void physics() {
		
		ParticleBase cell_below = viewport.GetParticle(ParticleCellPosition.X, ParticleCellPosition.Y + 1);
		ParticleBase cell_left = viewport.GetParticle(ParticleCellPosition.X - 1, ParticleCellPosition.Y);
		ParticleBase cell_right = viewport.GetParticle(ParticleCellPosition.X + 1, ParticleCellPosition.Y);
		
		bool canMoveDown = (cell_below == null);
		
		if (cell_below != null) {
			canMoveDown = cell_below.Density < Density;
		}

		bool canMoveLeft = (cell_left == null);
		
		if (cell_left != null) {
			canMoveLeft = cell_left.Density < Density;
		}

		bool canMoveRight = (cell_right == null);
		
		if (cell_right != null) {
			canMoveRight = cell_right.Density < Density;
		}

		if (canMoveDown) ParticleVelocity.Y += 1;
		else {
			if (canMoveLeft) ParticleVelocity.X = -1;
			if (canMoveRight) ParticleVelocity.X = 1;
			if (canMoveLeft && canMoveRight) ParticleVelocity.X = RandRange(-1, 1);
		}
		if (ParticleVelocity != new Vector2(0f, 0f))
		{

			Vector2 VelocityVector = new Vector2(Mathf.Sign(ParticleVelocity.X), Mathf.Sign(ParticleVelocity.Y));
			ParticleBase ParticleInVelPlusPos = viewport.GetParticle((int)(ParticleCellPosition.X + VelocityVector.X), (int)(ParticleCellPosition.Y + VelocityVector.Y));
			if (ParticleInVelPlusPos == null) {
				viewport.SetParticle((int)(ParticleCellPosition.X), (int)(ParticleCellPosition.Y), null);
				viewport.SetParticle((int)(ParticleCellPosition.X + VelocityVector.X), (int)(ParticleCellPosition.Y + VelocityVector.Y), this);
			}
			else
			{
				if (ParticleInVelPlusPos is ParticleLiquid) 
				{
					viewport.SetParticle((int)(ParticleCellPosition.X), (int)(ParticleCellPosition.Y), ParticleInVelPlusPos);
					viewport.SetParticle((int)(ParticleCellPosition.X + VelocityVector.X), (int)(ParticleCellPosition.Y + VelocityVector.Y), this);
				}
			}

			ParticleVelocity -= VelocityVector;
		}
	}
}

public partial class ParticleGas : ParticleBase
{
public void physics() {
		
		ParticleBase cell_above = viewport.GetParticle(ParticleCellPosition.X, ParticleCellPosition.Y - 1);
		ParticleBase cell_below = viewport.GetParticle(ParticleCellPosition.X, ParticleCellPosition.Y + 1);
		ParticleBase cell_left = viewport.GetParticle(ParticleCellPosition.X - 1, ParticleCellPosition.Y);
		ParticleBase cell_right = viewport.GetParticle(ParticleCellPosition.X + 1, ParticleCellPosition.Y);
		
		bool canMoveUp = (cell_above == null);
		bool canMoveDown = (cell_below == null);
		bool canMoveLeft = (cell_left == null);
		bool canMoveRight = (cell_right == null);
		

		if (canMoveLeft && canMoveRight) ParticleVelocity.X = RandRange(-1, 1);

		if (canMoveUp) ParticleVelocity.Y -= 1;
		else {
			if (canMoveLeft) ParticleVelocity.X = -1;
			if (canMoveRight) ParticleVelocity.X = 1;
		}
		if (ParticleVelocity != new Vector2(0f, 0f))
		{

			Vector2 VelocityVector = new Vector2(Mathf.Sign(ParticleVelocity.X), Mathf.Sign(ParticleVelocity.Y));
			ParticleBase ParticleInVelPlusPos = viewport.GetParticle((int)(ParticleCellPosition.X + VelocityVector.X), (int)(ParticleCellPosition.Y + VelocityVector.Y));
			if (ParticleInVelPlusPos == null) {
				viewport.SetParticle((int)(ParticleCellPosition.X), (int)(ParticleCellPosition.Y), null);
				viewport.SetParticle((int)(ParticleCellPosition.X + VelocityVector.X), (int)(ParticleCellPosition.Y + VelocityVector.Y), this);
			}
			else
			{
				if (ParticleInVelPlusPos is ParticleGas) 
				{
					viewport.SetParticle((int)(ParticleCellPosition.X), (int)(ParticleCellPosition.Y), ParticleInVelPlusPos);
					viewport.SetParticle((int)(ParticleCellPosition.X + VelocityVector.X), (int)(ParticleCellPosition.Y + VelocityVector.Y), this);
				}
			}

			ParticleVelocity -= VelocityVector;
		}
		if (HasMethod("fire_physics")) Call("fire_physics");
	}

}

public partial class ParticleStone : ParticleBase
{
	public void init() {
		this.Density = 10f;
		this.CanMelt = true;
		this.ParticleMeltingTemperature = 200;
		this.ParticleColor = new Color(.5f, .5f, .5f);
	}
	public void ParticleUpdate() {
		float ColorValue = (ParticleTemperature / ParticleMeltingTemperature) / 100f;
		if (ParticleTemperature > 12) {
			ParticleTemperature -= ParticleTempDecay;
		}
		if (CanMelt) {
			ParticleColor = DefaultParticleColor + new Color(ColorValue, 0f, 0f);
		}
		for (int angle = 0; angle < 360; angle += 45) 
		{
			Vector2I position = ParticleCellPosition + (Vector2I)new Vector2(0, 1).Rotated(angle).Round();
			ParticleBase ParticleAtAngle = viewport.GetParticle(position.X, position.Y);
			if (ParticleAtAngle != null) {
				float temp_difference = (ParticleAtAngle.ParticleTemperature - ParticleTemperature);
				ParticleAtAngle.ParticleTemperature -= temp_difference / 100;
				ParticleTemperature += temp_difference / 100;
			}
		}
		if (ParticleTemperature > 1500) {
			ParticleLava replacement = new ParticleLava();
			replacement.ParticleTemperature = ParticleTemperature;
			viewport.CreateParticleAt(ParticleCellPosition.X, ParticleCellPosition.Y, replacement, ParticleVelocity);
		}
	}
}

public partial class ParticleSand: ParticlePowder
{
	public void init() {
		this.ParticleColor = new Color("#C2B280");
		this.Density = 1f;
	}
}


public partial class ParticleWater: ParticleLiquid
{
	public void init() {
		float value = (float)RandRange(-0.1f, 0.1f);
		this.Density = 0.5f;
		this.ParticleColor = new Color("#2328cc") + new Color(value, value, value);
	}
		public void ParticleInit() {
		DefaultParticleColor = ParticleColor;
	}
	public void ParticleUpdate() {
		for (int angle = 0; angle < 360; angle += 45) 
		{
			Vector2I position = ParticleCellPosition + (Vector2I)new Vector2(0, 1).Rotated(angle).Round();
			ParticleBase ParticleAtAngle = viewport.GetParticle(position.X, position.Y);
			if (ParticleAtAngle != null) {
				float temp_difference = ParticleAtAngle.ParticleTemperature - ParticleTemperature;
				ParticleAtAngle.ParticleTemperature -= temp_difference / 10;
				ParticleTemperature += temp_difference / 10;
			}
		}
		if (ParticleTemperature > 100) {
			ParticleSteam replacement = new ParticleSteam();
			replacement.ParticleTemperature = ParticleTemperature;
			viewport.CreateParticleAt(ParticleCellPosition.X, ParticleCellPosition.Y, replacement, ParticleVelocity);
		}
	}
}

public partial class ParticleOil: ParticleLiquid
{
	public void init() {
		float value = (float)RandRange(-0.1f, 0.1f);
		this.Density = 0.25f;
		this.ParticleColor = new Color("#464d3e") + new Color(value, value, value);
		this.IsFlammable = true;
	}
}

public partial class ParticleLava: ParticleLiquid
{
	public void init() {
		float value = (float)RandRange(-0.1f, 0.1f);
		this.Density = 1.5f;
		this.ParticleColor = new Color("#FF2600") + new Color(value, value, value);
		
	}
	public void ParticleUpdate() {

			for (int angle = 0; angle < 360; angle += 45) 
		{
			Vector2I position = ParticleCellPosition + (Vector2I)new Vector2(0, 1).Rotated(angle).Round();
			ParticleBase ParticleAtAngle = viewport.GetParticle(position.X, position.Y);
			if (ParticleAtAngle != null) {
				float temp_difference = ParticleAtAngle.ParticleTemperature - ParticleTemperature;
				ParticleAtAngle.ParticleTemperature -= temp_difference / 10;
				ParticleTemperature += temp_difference / 10;
			}
		}
		if (ParticleTemperature < 1500) {
			ParticleStone replacement = new ParticleStone();
			replacement.ParticleTemperature = ParticleTemperature;
			viewport.CreateParticleAt(ParticleCellPosition.X, ParticleCellPosition.Y, replacement, ParticleVelocity);
		}
	}
}

public partial class ParticleSteam : ParticleGas
{
	public void init() {
		float value = (float)RandRange(-0.1f, 0.1f);
		this.Density = 0.15f;
		this.ParticleTempDecay = 0.2f;
		this.ParticleColor = new Color("#6786db") + new Color(value, value, value);
	}
	public void ParticleUpdate() {
		if (ParticleTemperature < 100) {
			ParticleWater replacement = new ParticleWater();
			replacement.ParticleTemperature = ParticleTemperature;
			viewport.CreateParticleAt(ParticleCellPosition.X, ParticleCellPosition.Y, replacement, ParticleVelocity);
		}
		if (ParticleTemperature > 12) {
			ParticleTemperature -= ParticleTempDecay;
		}
	}

}

public partial class ParticleFire : ParticleGas
{
	public void init() {
		this.Density = .15f;
		this.ParticleColor = new Color("#000000");
	}

	public void fire_physics() 
	{
		for (int angle = 0; angle < 360; angle += 45) 
		{
			for (int distance = 0; distance < 3; distance++) {
		
				Vector2I position = ParticleCellPosition + (Vector2I)new Vector2(0, 1).Rotated(angle).Round() * distance;
				ParticleBase ParticleAtAngle = viewport.GetParticle(position.X, position.Y);
				if (ParticleAtAngle != null) 
				{
					if (ParticleAtAngle.IsFlammable) {
						ParticleFire newFire = (ParticleFire)this.Duplicate();
						newFire.Decay = 0f;
						viewport.CreateParticleAt(position.X, position.Y, (ParticleBase) newFire, new Vector2(0,0));
					}
					if (ParticleAtAngle is ParticleFire == false) {
						ParticleAtAngle.ParticleTemperature += (ParticleTemperature / 10f);
						// PrintS(ParticleAtAngle.ParticleTemperature);
					}
				}

			}
		}
		Decay += 0.1f;
		ParticleTemperature = 1500f / (Decay * 10f);
		float v = Mathf.Clamp(Decay, 0f, 1f);
		this.ParticleColor = (new Color("#FF8C00") * new Color(v, v, v)) + (new Color("#FFFFFF") * new Color(v * 0.25f, v * 0.25f, v * 0.25f));
		this.DefaultParticleColor = ParticleColor;
		if (Decay >= 1f) {
			viewport.SetParticle(ParticleCellPosition.X, ParticleCellPosition.Y, null);
		}
	}
}

