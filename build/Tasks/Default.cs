using Cake.Frosting;

namespace Build.Tasks
{
	[IsDependentOn(typeof(Zip))]
	public sealed class Default : FrostingTask<Context>
	{
	}
}