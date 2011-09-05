<?php
defined('_JEXEC') or die('Restricted access');
jimport('joomla.application.component.controlleradmin');

class DbAppControllerSequencingBatches extends JControllerAdmin {

  function display($cachable = false) {
    // set default view if not set
    JRequest::setVar('view', JRequest::getCmd('view', 'SequencingBatches'));
    JRequest::setVar('task', JRequest::getCmd('display', 'SequencingBatches'));
    // call parent behavior
    parent::display($cachable);
    // Set the submenu
    DbAppHelper::addSubmenu('messages');
  }

	public function getModel($name = 'SequencingBatches', $prefix = 'DbAppModel') {
		$model = parent::getModel($name, $prefix, array('ignore_request' => true));
		return $model;
	}

}
