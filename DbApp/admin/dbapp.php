<?php
defined('_JEXEC') or die('Restricted access');
if (!JFactory::getUser()->authorise('core.manage', 'com_dbapp')) {
	return JError::raiseWarning(404, JText::_('JERROR_ALERTNOAUTHOR'));
}
/*
// Check to ensure this file is included in Joomla!
defined('_JEXEC') or die('Restricted Access');
*/


// require helper file
JLoader::register('DbAppHelper', dirname(__FILE__) . DS . 'helpers' . DS . 'dbapp.php');
// import joomla controller library
jimport('joomla.application.component.controller'); 
// get the base controller
// Get an instance of the controller prefixed by DbApp

//$mycid  = JRequest::getVar( 'cid', array(0), 'post', 'array' );
//$mc = JRequest::getVar( 'c' );
//$mytask = JRequest::getVar( 'task' );
//$myviews = JRequest::getWord( 'views' );
//JError::raiseWarning('500', JText::_(' task: ' . $mytask . ' | c: ' . $mc . ' | cid: ' . $mycid[0] . ' | views: ' . $myviews . ' |'));

require_once(JPATH_COMPONENT.DS.'controller.php');
require_once(JPATH_COMPONENT.DS.'controllers'.DS.'clients.php');
require_once(JPATH_COMPONENT.DS.'controllers'.DS.'projects.php');
require_once(JPATH_COMPONENT.DS.'controllers'.DS.'managers.php');

require_once(JPATH_COMPONENT.DS.'controllers'.DS.'sequencingbatch.php');
require_once(JPATH_COMPONENT.DS.'controllers'.DS.'lane.php');
require_once(JPATH_COMPONENT.DS.'controllers'.DS.'illuminarun.php');
require_once(JPATH_COMPONENT.DS.'controllers'.DS.'sequencingprimer.php');
//require_once(JPATH_COMPONENT.DS.'models'.DS.'forms'.DS.'contacts.php');
//JLoader::register('DbAppControllerTabOne', dirname(__FILE__) . DS . 'controllers' . DS . 'tabones.php');
//JLoader::register('DbAppController', dirname(__FILE__) . DS . 'controllers' . DS . 'tabtwos.php');

$controller = JController::getInstance('DbApp');
// Perform the Request task
//$controller->execute(JRequest::getCmd('task'));
//$c = JRequest::getCmd('view', 'TabOnes');

//JError::raiseWarning('500', JText::_('[from admin/dbapp.php]C ' . $c . ' < inget > |'));
//JError::raiseWarning('500', JText::_('[from admin/dbapp.php]task ' . JRequest::getCmd('task') . ' < inget > |'));

//$controller = DbAppController::gogetInstance('TabOnes');

//JError::raiseWarning('500', JText::_('[from admin/dbapp.php]view ' . JRequest::getCmd('view') . ' < do controller > ' . $controller . ' |'));

$controller->execute(JRequest::getCmd('task', 'display'));
// redirect
$controller->redirect();
