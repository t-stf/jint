// Copyright 2009 the Sputnik authors.  All rights reserved.
/**
 * If x is +Infinity, Math.round(x) is +Infinity
 *
 * @path ch15/15.8/15.8.2/15.8.2.15/S15.8.2.15_A4.js
 * @description Checking if Math.round(x) is +Infinity, where x is +Infinity
 */

// CHECK#1
var x = +Infinity;
if (Math.round(x) !== +Infinity)
{
	$ERROR("#1: 'var x=+Infinity; Math.round(x) !== +Infinity'");
}

