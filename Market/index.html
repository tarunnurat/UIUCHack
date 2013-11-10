<!DOCTYPE html>
<html lang="en">
	<style type="text/css">
		div.scroll
		{
		background-color:#FFFFFF;
		width:500px;
		height:500px;
		overflow:scroll;
}

	</style>
  <head>
    <meta charset="utf-8">
    <title>Harsha Market</title>
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <meta name="description" content="">
    <meta name="author" content="">
<!-- styles -->
	<link rel="stylesheet/less" type="text/css" href="themes/less/bootstrap.less">
	<script src="themes/js/less/less.js" type="text/javascript"></script>

	<!-- favicon-icons -->
    <link rel="shortcut icon" href="themes/images/favicon.ico">
  </head>
<body>
<header class="header">
<div class="container">
<div class="row">
	<div class="offset6 span6 right-align loginArea">
		<a href="#login" role="button" data-toggle="modal"><span class="btn btn-mini"> Login  </span></a> 
		<a href="register.php"><span class="btn btn-mini btn-success"> Register  </span></a> 
		<a href="checkout.php"><span class="btn btn-mini btn-danger"> Cart </span></a> 
	</div>
</div>

<!-- Login Block -->
<div id="login" class="modal hide fade in" tabindex="-1" role="dialog" aria-labelledby="login" aria-hidden="false" >
  <div class="modal-header">
	<button type="button" class="close" data-dismiss="modal" aria-hidden="true">×</button>
	<h3>Sell Anythings : Login Block</h3>
  </div>
  <div class="modal-body">
	<form class="form-horizontal loginFrm">
	  <div class="control-group">								
		<input type="text" id="inputEmail" placeholder="Email">
	  </div>
	  <div class="control-group">
		<input type="password" id="inputPassword" placeholder="Password">
	  </div>
	  <div class="control-group">
		<label class="checkbox">
		<input type="checkbox"> Remember me
		</label>
	  </div>
	</form>		
	<button type="submit" class="btn btn-success">Sign in</button>
	<button class="btn" data-dismiss="modal" aria-hidden="true">Close</button>
  </div>
</div>

<div class="navbar">
<div class="navbar-inner">
	<a class="brand" href="index.php"><img src="" alt="Harsha Market"></a>
	<div class="nav-collapse">
		<ul id="topMenu" class="nav pull-right">
		 <li class="">
		 <form class="form-inline navbar-search" method="post" action="products.php" style="padding-top:5px;">
			<select class="span3" style="padding:11px 4px; height:auto">
				<option>All</option>
				<option>Clothes </option>
				<option>Women's Wear </option>
				<option>Men's Wear </option>
				<option>Kids Wear </option>
			</select> 
			<input class="span4" type="text" placeholder="eg. T-shirt" style="padding:11px 4px;">
			<button type="submit" class="btn btn-warning btn-large" style="margin-top:0"> GO </button>
		</form>
		</li>
		</ul>
	</div>
	<button type="button" class="btn btn-navbar" data-toggle="collapse" data-target=".nav-collapse">
		<span class="icon-bar"></span>
		<span class="icon-bar"></span>
		<span class="icon-bar"></span>
	</button>
  </div>
</div>
</div>
</header>
<!-- ======================================================================================================================== -->
<section id="mainBody">
<div class="container">
<h3 class="title"><span>ITEM DETAILS</span></h3>
<div style="width:100%;"> 
	<div style="float:left; width:50%;">
  	<script type="text/javascript" src="jquery-1.10.2.js"></script>	
	<script type="text/javascript" src="glmatrix.js"></script>	
	<script type="text/javascript" src="obj-module.js"></script>	
	<script type="text/javascript" src="webgl-utils.js"></script>
	<script id = "shader-fs" type="x-shader/x-fragment">
	  	precision mediump float;

		varying vec2 vTextureCoord;
		varying vec2 vReflectiveTextureCoord;
		varying vec3 vPosition;
		varying vec3 vNormal;

		uniform sampler2D uSampler;
		uniform sampler2D uSampler2;

		uniform vec3 lightPosition;	

		uniform vec3 uAmbientColor;

		void main(void) {
   			vec3 defaultLight = vec3(.8,.8,.8);
   			vec3 lightIntensity;
   			vec4 roughTextureColor = texture2D(uSampler2, vec2(vTextureCoord.s, vTextureCoord.t));
   			vec4 reflectTextureColor = texture2D(uSampler, vec2(vReflectiveTextureCoord.s, vReflectiveTextureCoord.t));
   			vec3 textureTotals = roughTextureColor.rgb*1.5 * reflectTextureColor.rgb*1.5;

    		vec3 eyeDirection = normalize(-vPosition);
    		
   			vec3 lightDirectionWithRespectToVertex = -normalize(vPosition - lightPosition);
   			vec3 reflectionDirection = reflect(-lightDirectionWithRespectToVertex, vNormal);

   			float directionalLightWeighting = max(dot(vNormal, lightDirectionWithRespectToVertex), 0.0);

   			float specularLightWeighting = pow(max(dot(reflectionDirection, eyeDirection), 0.0), 25.0);

   			lightIntensity = uAmbientColor + defaultLight * directionalLightWeighting + defaultLight * 3.0 * specularLightWeighting;
    		//gl_FragColor = vec4(textureTotals * vLightWeighting + vec3(0.6,0.6,0.6) * specularLightWeighting, roughTextureColor.a);
    		gl_FragColor = vec4(textureTotals * lightIntensity, roughTextureColor.a);
		}
	</script>

	<script id = "shader-vs" type="x-shader/x-vertex">
		attribute vec3 aVertexPosition;
		attribute vec3 aVertexNormal;
		attribute vec2 aTextureCoord;

		uniform mat4 uMVMatrix;
		uniform mat4 uPMatrix;
		uniform mat3 uNMatrix;

		uniform vec3 lightPosition;

		uniform vec3 uAmbientColor;

		uniform vec3 uLightingDirection;
		uniform vec3 uDirectionalColor;

		varying vec2 vTextureCoord;
		varying vec2 vReflectiveTextureCoord;
		//varying vec3 vLightWeighting;
		varying vec3 vPosition;
		varying vec3 vNormal;

		void main(void) {
		vPosition = (uMVMatrix * vec4(aVertexPosition, 1.0)).xyz;
		gl_Position = uPMatrix * uMVMatrix * vec4(aVertexPosition, 1.0);
		vec3 transformedNormal = uNMatrix * aVertexNormal;
		vNormal = transformedNormal;
		vTextureCoord = aTextureCoord;
		vReflectiveTextureCoord = vec2(0.5, 0.5) + vec2(transformedNormal[0]*.5, transformedNormal[1]*.5);
        
		}
	</script>

	<script type="text/javascript">
		var modelURL = "teapot.obj";
		$( document ).ready( function(){
			 webGLStart();
		});
	</script>
	<body>
		<canvas id="my-canvas" width="500px" height="500px">
		</canvas>
	</div>
	<div style="float:right; width:50%;">
		<div class="scroll">
		<table>
			<tr>
				<td> <a href=""><img src="Hat.png" width="100" height="100"></a></td>
				<td> This item is an awesome hat that looks awesome </td>
			</tr>
			<tr>
				<td> <a href=""><img src="Hat.png" width="100" height="100"></a></td>
				<td> This item is an awesome hat that looks awesome </td>
			</tr>
			<tr>
				<td> <a href=""><img src="Hat.png" width="100" height="100"></a></td>
				<td> This item is an awesome hat that looks awesome </td>
			</tr>
			<tr>
				<td> <a href=""><img src="Hat.png" width="100" height="100"></a></td>
				<td> This item is an awesome hat that looks awesome </td>
			</tr>
			<tr>
				<td> <a href=""><img src="Hat.png" width="100" height="100"></a></td>
				<td> This item is an awesome hat that looks awesome </td>
			</tr>
			<tr>
				<td> <a href=""><img src="Hat.png" width="100" height="100"></a></td>
				<td> This item is an awesome hat that looks awesome </td>
			</tr>
		</table>
	</div>
</div>
</div>
</div>
</div>

