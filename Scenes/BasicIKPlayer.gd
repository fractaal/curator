extends SkeletonIK3D

var targetNode: Node3D

var basePosition
var baseRotation

var noiseX = FastNoiseLite.new()
var noiseY = FastNoiseLite.new()
var noiseZ = FastNoiseLite.new()
var skeleton: Skeleton3D

@export var modulateParameter = "position";
@export var modulationIntensity = 0.1
@export var modulationRate = 0.025

func _ready():
	start()
	
	skeleton = get_node("../")

	targetNode = get_node(target_node)

	basePosition = targetNode[modulateParameter]

	noiseX.seed = targetNode.position.x * 128
	noiseY.seed = targetNode.position.y * 512
	noiseZ.seed = targetNode.position.z * 64

func _process(delta):

	var time = Time.get_ticks_msec() * modulationRate

	var x = noiseX.get_noise_1d(time);
	var y = noiseY.get_noise_1d(time) + (skeleton.position.y * 0.5);
	var z = noiseZ.get_noise_1d(time);

	targetNode[modulateParameter] = basePosition + (Vector3(x, y, z) * modulationIntensity)
