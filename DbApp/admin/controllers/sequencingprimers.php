<?php
defined('_JEXEC') or die('Restricted access');
jimport('joomla.application.component.controlleradmin');

class DbAppControllerSequencingPrimers extends JControllerAdmin {

  function display($cachable = false) {
    // set default view if not set
    JRequest::setVar('view', JRequest::getCmd('view', 'SequencingPrimers'));
    JRequest::setVar('task', JRequest::getCmd('display', 'SequencingPrimers'));
    // call parent behavior
    parent::display($cachable);
    // Set the submenu
    DbAppHelper::addSubmenu('messages');
  }

	public function getModel($name = 'SequencingPrimer', $prefix = 'DbAppModel') {
		$model = parent::getModel($name, $prefix, array('ignore_request' => true));
		return $model;
	}

}
