var app = angular.module('codeLeagueApp', ['ngAnimate']);

app.controller('rankCtrl', function($scope, $http, $interval) {
    
    getTeamFunc = function(){

    	$http.get("api/teams").success(

	    	function(response) {

	    		$scope.teams = response.teams;


	    		for(var i = 0; i< $scope.teams.length; i++){

	    			$scope.teams[i].keep = false;
	    			$scope.teams[i].up = false;
	    			$scope.teams[i].down = false;

	    			if($scope.teams[i].change.includes('---')){

	    				$scope.teams[i].keep = true;
	    				$scope.teams[i].changeValue = "---";

	    			}else if($scope.teams[i].change.includes('+')){

	    				$scope.teams[i].up = true;
	    				$scope.teams[i].changeValue = $scope.teams[i].change.substring(1);

	    			}else{

	    				$scope.teams[i].down = true;
	    				$scope.teams[i].changeValue = $scope.teams[i].change.substring(1);

	    			}
	    			
	    		}
	    		

	    	}
    )};

    viewSwitchFunc = function(){

    	$scope.generalView = !$scope.generalView;

    	if($scope.generalView){

    		$interval(viewSwitchFunc,100000,1);

    	}else{

    		$interval(viewSwitchFunc,6000,1);
    	}

    };

    getTeamFunc();

   	$scope.generalView = true;

    $interval(getTeamFunc,5000);

    $interval(viewSwitchFunc,100000,1);


    
});