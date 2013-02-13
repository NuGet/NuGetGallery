function require-param {
  param($value, $paramName)
  if ($value -eq $null) {
    write-error "The parameter -$paramName is required."
	$false;
  } else {
	$true
  }
}