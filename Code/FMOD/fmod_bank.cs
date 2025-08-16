namespace FMODStudio
{
	class Bank
	{
		public FMOD.Studio.Bank bank;
		public FMOD.Studio.Bank stringsBank;

		public Bank(FMOD.Studio.Bank bank, FMOD.Studio.Bank stringsBank)
		{
			this.bank = bank;
			this.stringsBank = stringsBank;
		}
	}
}