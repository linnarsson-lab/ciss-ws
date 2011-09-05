<?php

defined('_JEXEC') or die('Restricted access');
jimport('joomla.application.component.controller');


class DbAppController extends JController {

//  function __construct() {
//    $path = JPATH_COMPONENT_ADMINISTRATOR.DS.'models';
//    $this->addModelPath($path);
//    $path = JPATH_COMPONENT_ADMINISTRATOR.DS.'views';
//    $this->addViewPath($path);
//    parent::__construct();
//  }

  function display() {
    parent::display();
  }



  function gogetInstance($entity, $prefix='DbAppController') {
// use a static array to store controller instances
    static $instances;
      if (!$instances) {
        $instances = array();
      }
// determine subclass name
    $class = $prefix.ucfirst($entity);

// check if we already instantiated this controller
    if (!isset($instances[$class])) {
// check if we need to find the controller class
      if (!class_exists( $class )) {
        jimport('joomla.filesystem.file');
        $path = JPATH_COMPONENT.DS.'controllers'.DS. JString::strtolower($entity) . '.php';
// search for the file in the controllers path
        if (JFile::exists($path)) {
// include the class file
echo JError::raiseWarning(500, '[from controller.php]controllerpath < '. $path . ' >');

          require_once $path;

          if (!class_exists($class)) {
// class file does not include the class
            return JError::raiseWarning('SOME_ERROR', JText::_('[from controller.php]Invalid controller l.47'));
          }
        } else {
// class file not found
          return JError::raiseWarning('SOME_ERROR', JText::_('[from controller.php]Unknown controller ' . $path . ' l.51'));
        }
      }
// create controller instance
      $instances[$class] = new $class();
    }
// return a reference to the controller
    return $instances[$class];
  }

}
