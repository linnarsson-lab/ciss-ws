<?php

defined('_JEXEC') or die('Restricted access');
jimport('joomla.application.component.controller');
jimport('joomla.filesystem.file');
jimport('joomla.application.component.helper');

// Require the base controller
require_once (JPATH_COMPONENT.DS.'controller.php');

// Require specific controller if requested
if ($controller = JRequest::getVar('controller')) {
  require_once (JPATH_COMPONENT.DS.'controllers'.DS.$controller.'.php');
}

// Create the controller
$classname  = 'DbAppController'.$controller;
$controller = new $classname();

// Perform the Request task
$controller->execute( JRequest::getVar('task'));

// Redirect if set by the controller
$controller->redirect();



///////////////////////////////////////////////////////////////////////////////////////////

//$controller = JController::getInstance('DbApp');

 
 
 // Load the toolbar helper
//require_once( JPATH_COMPONENT_ADMINISTRATOR.DS.'helpers'.DS.'toolbar.php' );
 
 // render the toolbar on the page. rendering it here means that it is displayed on every view of your component.
//echo DbAppHelperToolbar::getToolbar();
//require_once(JPATH_COMPONENT.DS
//require_once(JPATH_COMPONENT.DS.'controller.php');
//require_once(JPATH_COMPONENT.DS.'controllers'.DS.'clients.php');
//require_once(JPATH_COMPONENT.DS.'controllers'.DS.'samples.php');

// get controller
//if ($c = JRequest::getCmd('c')) {
  // determine path
//  $path = JPATH_COMPONENT.DS.'controllers'.DS.$c.'.php';
//  if (JFile::exists($path)) {
    // controller exists, get it!
//    require_once($path);
//  } else {
    // controller does not exist
//    JError::raiseError('500', JText::_('Unknown controller: ' . $path));
//  }
//}

// instantiate and execute the controller
//$c = 'DbappController'.$c;
//$controller = new $c();


//$controller->execute(JRequest::getCmd('task'));

//$controller->redirect();
