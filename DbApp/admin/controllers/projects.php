<?php
defined('_JEXEC') or die('Restricted access');
jimport('joomla.application.component.controlleradmin');

class DbAppControllerProjects extends JControllerAdmin {

  function display($cachable = false) {
    // set default view if not set
echo JError::raiseWarning(500, JText::_('from controller Projects !'));
    JRequest::setVar('view', JRequest::getCmd('view', 'Projects'));
    JRequest::setVar('task', JRequest::getCmd('display', 'Projects'));
    // call parent behavior
    parent::display($cachable);
    // Set the submenu
    DbAppHelper::addSubmenu('messages');
  }

	public function getModel($name = 'Project', $prefix = 'DbAppModel') {
		$model = parent::getModel($name, $prefix, array('ignore_request' => true));

//echo JError::raiseWarning(500, JText::_('messageid '. $name . ' detta är tvåan'));
//  denna sträng dök upp efter en delete

		return $model;
	}

}
